using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using DotNetEnv;
using ScottPlot;

// 1. Initialize Market Prices Dictionary
var marketPrices = new Dictionary<(string Commodity, string DeliveryMonth), double>
{
    [("Natural Gas", "August 2-23rd")] = 3.50,
    [("Natural Gas", "September 2-23rd")] = 3.60,
    [("Power", "August 2-23rd")] = 52.00
};

// Daily Volatility per commodity bucket
const double dailyVolatility = 0.025; 

// 2. Load .env and build PostgreSQL Connection
Env.TraversePath().Load();

var builder = new NpgsqlConnectionStringBuilder
{
    Host = Environment.GetEnvironmentVariable("DB_HOST"),
    Port = int.TryParse(Environment.GetEnvironmentVariable("DB_PORT"), out var port) ? port : 5432,
    Database = Environment.GetEnvironmentVariable("DB_NAME"),
    Username = Environment.GetEnvironmentVariable("DB_USER"),
    Password = Environment.GetEnvironmentVariable("DB_PASSWORD")
};

using var conn = new NpgsqlConnection(builder.ConnectionString);
conn.Open();

Console.WriteLine("Successfully connected to PostgreSQL!");

CreateTable(conn);
AnalyzeExistingTrades(conn, marketPrices, dailyVolatility);
DisplayGraph(conn);

// ==========================================
// CALCULATIONS & HELPER METHODS
// ==========================================

static void CreateTable(NpgsqlConnection connection)
{
    const string sql = """
        CREATE TABLE IF NOT EXISTS trades (
            id SERIAL PRIMARY KEY,
            party TEXT NOT NULL,
            counterparty TEXT NOT NULL,
            commodity TEXT NOT NULL,
            volume DOUBLE PRECISION NOT NULL,
            price DOUBLE PRECISION NOT NULL,
            delivery_month TEXT NOT NULL,
            buy_sell TEXT NOT NULL
        );
        """;

    using var cmd = new NpgsqlCommand(sql, connection);
    cmd.ExecuteNonQuery();
}

static List<Trade> GetAllTrades(NpgsqlConnection connection)
{
    var tradesList = new List<Trade>();
    const string sql = "SELECT id, party, counterparty, commodity, volume, price, delivery_month, buy_sell FROM trades;";

    using var cmd = new NpgsqlCommand(sql, connection);
    using var reader = cmd.ExecuteReader();

    while (reader.Read())
    {
        tradesList.Add(new Trade(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetDouble(4),
            reader.GetDouble(5),
            reader.GetString(6),
            reader.GetString(7)
        ));
    }

    return tradesList;
}

static double CalculateBucketVar(double netPosition, double currentPrice, double volatility, double zScore = 1.645) =>
    Math.Abs(netPosition) * currentPrice * volatility * zScore;

static void WriteNewTrade(NpgsqlConnection connection, Trade newTrade)
{
    CreateTable(connection);
    const string sql = """
        INSERT INTO trades (party, counterparty, commodity, volume, price, delivery_month, buy_sell) 
        VALUES (@party, @counterparty, @commodity, @volume, @price, @delivery_month, @buy_sell)
        """;

    using var cmd = new NpgsqlCommand(sql, connection);
    cmd.Parameters.AddWithValue("party", newTrade.Party);
    cmd.Parameters.AddWithValue("counterparty", newTrade.CounterParty);
    cmd.Parameters.AddWithValue("commodity", newTrade.Commodity);
    cmd.Parameters.AddWithValue("volume", newTrade.Volume);
    cmd.Parameters.AddWithValue("price", newTrade.Price);
    cmd.Parameters.AddWithValue("delivery_month", newTrade.DeliveryMonth);
    cmd.Parameters.AddWithValue("buy_sell", newTrade.BuySell);

    cmd.ExecuteNonQuery();
    Console.WriteLine("Recorded new trade to database.");
}

// Idiomatic LINQ calculations replacing imperative foreach loops
static double CalculateBucketPosition(IEnumerable<Trade> bucketTrades) =>
    bucketTrades.Sum(t => string.Equals(t.BuySell, "Buy", StringComparison.OrdinalIgnoreCase) ? t.Volume : -t.Volume);

static double CalculateBucketMtmPnl(IEnumerable<Trade> bucketTrades, double marketPrice, double netPos)
{
    double totalCashFlow = bucketTrades.Sum(t => 
        string.Equals(t.BuySell, "Buy", StringComparison.OrdinalIgnoreCase)  ? -t.Volume * t.Price :
        string.Equals(t.BuySell, "Sell", StringComparison.OrdinalIgnoreCase) ?  t.Volume * t.Price : 0);

    return totalCashFlow + (netPos * marketPrice);
}

static double CalculateBucketExposure(double netPos) => netPos * 0.01;

static void AnalyzeExistingTrades(NpgsqlConnection connection, Dictionary<(string Commodity, string DeliveryMonth), double> marketPrices, double dailyVolatility)
{
    var trades = GetAllTrades(connection);

    Console.WriteLine($"\nLoaded {trades.Count} trades from database:");
    foreach (var trade in trades)
    {
        Console.WriteLine($"Trade ID: {trade.TradeID} | {trade.Commodity} | {trade.DeliveryMonth}");
    }

    var riskBuckets = trades
        .Select(t => (t.Commodity, Month: t.DeliveryMonth))
        .Distinct()
        .OrderBy(b => b.Commodity)
        .ThenBy(b => b.Month);

    Console.WriteLine("\n=== MULTI-COMMODITY RISK & P&L REPORT ===");

    double totalPortfolioVar = 0.0;

    foreach (var (commodity, month) in riskBuckets)
    {
        var bucketTrades = trades
            .Where(t => t.Commodity == commodity && t.DeliveryMonth == month)
            .ToList();

        marketPrices.TryGetValue((commodity, month), out var marketPrice);

        var netPos = CalculateBucketPosition(bucketTrades);
        var bucketPnl = CalculateBucketMtmPnl(bucketTrades, marketPrice, netPos);
        var bucketExposure = CalculateBucketExposure(netPos);
        var bucketVar = CalculateBucketVar(netPos, marketPrice, dailyVolatility);
        
        totalPortfolioVar += bucketVar;
        var unit = commodity == "Power" ? "MWh" : "MMBtu";

        Console.WriteLine($"\n[{commodity}] - {month}");
        Console.WriteLine($"   Net Position:  {netPos:N0} {unit}");
        Console.WriteLine($"   MTM P&L:       ${bucketPnl:N2}");
        Console.WriteLine($"   Risk Exposure: ${bucketExposure:N2} per 1¢ move");
        Console.WriteLine($"   1-Day 95% VaR: ${bucketVar:N2}");
    }

    Console.WriteLine("\n-----------------------------------------");
    Console.WriteLine($"Total Undiversified Portfolio VaR: ${totalPortfolioVar:N2}");
    Console.WriteLine("-----------------------------------------");
}

static void DisplayGraph(NpgsqlConnection connection)
{
    var trades = GetAllTrades(connection);
    if (trades.Count == 0)
    {
        Console.WriteLine("No trades found to plot.");
        return;
    }

    var bucketData = trades
        .GroupBy(t => (t.Commodity, Month: t.DeliveryMonth))
        .ToDictionary(g => g.Key, g => g.ToList());

    var monthOrder = new Dictionary<string, int>
    {
        ["August 2-23rd"] = 1,
        ["September 2-23rd"] = 2,
        ["October 2-23rd"] = 3
    };

    var gasData = new List<(string Month, double Pos)>();
    var powerData = new List<(string Month, double Pos)>();

    foreach (var (key, value) in bucketData)
    {
        var netPos = CalculateBucketPosition(value);
        if (key.Commodity == "Natural Gas")
            gasData.Add((key.Month, netPos));
        else if (key.Commodity == "Power")
            powerData.Add((key.Month, netPos));
    }

    gasData = gasData.OrderBy(x => monthOrder.GetValueOrDefault(x.Month, 99)).ToList();
    powerData = powerData.OrderBy(x => monthOrder.GetValueOrDefault(x.Month, 99)).ToList();

    var plt = new ScottPlot.Plot();
    plt.Title("Net Position Book Exposure by Commodity and Month");
    plt.XLabel("Delivery Month");

    if (gasData.Count > 0)
    {
        var xs = Enumerable.Range(0, gasData.Count).Select(i => (double)i).ToArray();
        var ys = gasData.Select(x => x.Pos).ToArray();
        var sig = plt.Add.Scatter(xs, ys);
        sig.LegendText = "Natural Gas";
        
        var labels = gasData.Select(x => x.Month).ToArray();
        plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(xs, labels);
    }

    const string outputFile = "book_exposure.png";
    plt.SavePng(outputFile, 800, 600);
    Console.WriteLine($"\nGraph successfully generated and saved to: {outputFile}");
}