using System;
using System.Collections.Generic;
using System.Linq;
using Npgsql;
using DotNetEnv;
using ScottPlot;

class Program
{
    static void Main(string[] args)
    {
        var marketPrices = new Dictionary<(string Commodity, string DeliveryMonth), double>
        {
            [("Natural Gas", "August 2-23rd")] = 3.50,
            [("Natural Gas", "September 2-23rd")] = 3.60,
            [("Power", "August 2-23rd")] = 52.00
        };

        var seedPriceHistory = new List<(string Date, string Commodity, string DeliveryMonth, double Price)>
        {
            // --- Natural Gas: August 2-23rd ---
            ("2026-08-03", "Natural Gas", "August 2-23rd", 3.45),
            ("2026-08-04", "Natural Gas", "August 2-23rd", 3.50),
            ("2026-08-05", "Natural Gas", "August 2-23rd", 3.48),
            ("2026-08-06", "Natural Gas", "August 2-23rd", 3.55),
            ("2026-08-07", "Natural Gas", "August 2-23rd", 3.60),
            ("2026-08-10", "Natural Gas", "August 2-23rd", 3.58),
            ("2026-08-11", "Natural Gas", "August 2-23rd", 3.62),
            ("2026-08-12", "Natural Gas", "August 2-23rd", 3.65),
            ("2026-08-13", "Natural Gas", "August 2-23rd", 3.59),
            ("2026-08-14", "Natural Gas", "August 2-23rd", 3.54),
            ("2026-08-17", "Natural Gas", "August 2-23rd", 3.52),
            ("2026-08-18", "Natural Gas", "August 2-23rd", 3.57),
            ("2026-08-19", "Natural Gas", "August 2-23rd", 3.61),
            ("2026-08-20", "Natural Gas", "August 2-23rd", 3.63),
            ("2026-08-21", "Natural Gas", "August 2-23rd", 3.60),

            // --- Natural Gas: September 2-23rd ---
            ("2026-08-03", "Natural Gas", "September 2-23rd", 3.55),
            ("2026-08-04", "Natural Gas", "September 2-23rd", 3.58),
            ("2026-08-05", "Natural Gas", "September 2-23rd", 3.62),
            ("2026-08-06", "Natural Gas", "September 2-23rd", 3.60),
            ("2026-08-07", "Natural Gas", "September 2-23rd", 3.66),
            ("2026-08-10", "Natural Gas", "September 2-23rd", 3.70),
            ("2026-08-11", "Natural Gas", "September 2-23rd", 3.68),
            ("2026-08-12", "Natural Gas", "September 2-23rd", 3.72),
            ("2026-08-13", "Natural Gas", "September 2-23rd", 3.69),
            ("2026-08-14", "Natural Gas", "September 2-23rd", 3.65),
            ("2026-08-17", "Natural Gas", "September 2-23rd", 3.61),
            ("2026-08-18", "Natural Gas", "September 2-23rd", 3.64),
            ("2026-08-19", "Natural Gas", "September 2-23rd", 3.67),
            ("2026-08-20", "Natural Gas", "September 2-23rd", 3.70),
            ("2026-08-21", "Natural Gas", "September 2-23rd", 3.66),

            // --- Power: August 2-23rd ---
            ("2026-08-03", "Power", "August 2-23rd", 50.50),
            ("2026-08-04", "Power", "August 2-23rd", 51.20),
            ("2026-08-05", "Power", "August 2-23rd", 51.00),
            ("2026-08-06", "Power", "August 2-23rd", 52.30),
            ("2026-08-07", "Power", "August 2-23rd", 53.10),
            ("2026-08-10", "Power", "August 2-23rd", 52.80),
            ("2026-08-11", "Power", "August 2-23rd", 53.50),
            ("2026-08-12", "Power", "August 2-23rd", 54.00),
            ("2026-08-13", "Power", "August 2-23rd", 52.90),
            ("2026-08-14", "Power", "August 2-23rd", 51.80),
            ("2026-08-17", "Power", "August 2-23rd", 51.20),
            ("2026-08-18", "Power", "August 2-23rd", 52.00),
            ("2026-08-19", "Power", "August 2-23rd", 52.60),
            ("2026-08-20", "Power", "August 2-23rd", 53.00),
            ("2026-08-21", "Power", "August 2-23rd", 52.40)
        };

        var creditLimits = new Dictionary<string, double>
        {
            ["BP"] = 50000.00,
            ["Shell"] = 40000.00,
            ["Total"] = 30000.00
        };

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

        bool running = true;
        while (running)
        {
            // Build key-based measured volatilities directly from the DB price history
            var bucketVolatility = GetMeasuredVolatilities(conn);

            Console.WriteLine("\n=== ENERGY RISK ENGINE CLI ===");
            Console.WriteLine("1. Run Full Portfolio & Credit Risk Report");
            Console.WriteLine("2. Run Stress Test Scenarios");
            Console.WriteLine("3. Generate Book Exposure Graph");
            Console.WriteLine("4. Add New Trade");
            Console.WriteLine("5. Add Price History Entry");
            Console.WriteLine("6. View Price History Log");
            Console.WriteLine("7. Add Bulk Price History Data");
            Console.WriteLine("8. Run Volatility Verification Test");
            Console.WriteLine("9. Exit");
            Console.Write("\nSelect an option (1-9): ");

            var choice = Console.ReadLine();

            switch (choice)
            {
                case "1":
                    Console.Write("Enter Horizon in Days (e.g., 1 or 10): ");
                    if (!int.TryParse(Console.ReadLine(), out int horizon) || horizon <= 0) horizon = 1;

                    Console.Write("Enter Confidence Level % (95 or 99): ");
                    string confInput = Console.ReadLine() ?? "95";
                    double zScore = confInput.Trim() == "99" ? 2.326 : 1.645;

                    AnalyzeExistingTrades(conn, marketPrices, bucketVolatility, zScore, horizon);
                    AnalyzeCounterpartyExposure(conn, marketPrices, bucketVolatility, creditLimits);
                    break;
                case "2":
                    RunStressTestMenu(conn, marketPrices, bucketVolatility, creditLimits);
                    break;
                case "3":
                    DisplayGraph(conn);
                    break;
                case "4":
                    PromptAndWriteNewTrade(conn, marketPrices, bucketVolatility, creditLimits);
                    break;
                case "5":
                    PromptAndWriteHistoryTrade(conn);
                    break;
                case "6":
                    DisplayPriceHistory(conn);
                    break;
                case "7":
                    foreach (var record in seedPriceHistory)
                    {
                        WriteNewHistoryTrade(conn, record.Date, record.Commodity, record.DeliveryMonth, record.Price);
                    }
                    Console.WriteLine("Successfully added bulk price history data.");
                    break;
                case "8":
                    RunVolatilityVerificationTest(conn);
                    break;
                case "9":
                    running = false;
                    Console.WriteLine("Exiting Risk Engine.");
                    break;
                    
                default:
                    Console.WriteLine("Invalid option. Please enter 1-9.");
                    break;
            }
        }
    }

    // --- HISTORICAL VOLATILITY COMPUTATION ENGINE ---
    public static double CalculateHistoricalVolatility(NpgsqlConnection connection, string commodity, string deliveryMonth, int? maxRecords = null)
    {
        var historyQuery = GetPriceHistory(connection)
            .Where(h => string.Equals(h.Commodity, commodity, StringComparison.OrdinalIgnoreCase) && 
                        string.Equals(h.DeliveryMonth, deliveryMonth, StringComparison.OrdinalIgnoreCase))
            .OrderBy(h => h.Date)
            .Select(h => h.Price);

        if (maxRecords.HasValue)
        {
            historyQuery = historyQuery.Take(maxRecords.Value);
        }

        var prices = historyQuery.ToList();

        if (prices.Count < 2) 
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n[VOLATILITY WARNING] Insufficient price history for {commodity} ({deliveryMonth}). Defaulting to 2.50% fallback!");
            Console.ResetColor();
            return 0.025;
        }

        // 1. Daily Percentage Returns: (P_t - P_{t-1}) / P_{t-1}
        var returns = new List<double>();
        for (int i = 1; i < prices.Count; i++)
        {
            double dailyReturn = (prices[i] - prices[i - 1]) / prices[i - 1];
            returns.Add(dailyReturn);
        }

        // 2. Mean Return
        double mean = returns.Average();

        // 3. Sample Standard Deviation (N - 1)
        double sumOfSquaredDiffs = returns.Sum(r => Math.Pow(r - mean, 2));
        double sampleStdDev = Math.Sqrt(sumOfSquaredDiffs / (returns.Count - 1));

        return sampleStdDev;
    }

    public static Dictionary<(string Commodity, string DeliveryMonth), double> GetMeasuredVolatilities(NpgsqlConnection connection)
    {
        var history = GetPriceHistory(connection);
        var buckets = history.Select(h => (h.Commodity, h.DeliveryMonth)).Distinct();

        var dict = new Dictionary<(string Commodity, string DeliveryMonth), double>();
        foreach (var (commodity, month) in buckets)
        {
            dict[(commodity, month)] = CalculateHistoricalVolatility(connection, commodity, month);
        }

        return dict;
    }

    public static void RunVolatilityVerificationTest(NpgsqlConnection connection)
    {
        Console.WriteLine("\n=== TASK 2 VOLATILITY VERIFICATION ===");
        double testVol = CalculateHistoricalVolatility(connection, "Natural Gas", "September 2-23rd", maxRecords: 5);
        Console.WriteLine($"Hand Calculation (5-Day Sep NG) : 0.945%");
        Console.WriteLine($"Code Output (5-Day Sep NG)      : {testVol * 100:F3}%\n");

        Console.WriteLine("=== TASK 3 FULL 3-WEEK HISTORICAL VOLATILITY ===");
        var buckets = new[]
        {
            ("Natural Gas", "August 2-23rd"),
            ("Natural Gas", "September 2-23rd"),
            ("Power", "August 2-23rd")
        };

        foreach (var (commodity, month) in buckets)
        {
            double vol = CalculateHistoricalVolatility(connection, commodity, month);
            Console.WriteLine($"{commodity,-12} | {month,-18} | Volatility: {vol * 100:F3}% ({vol:F5})");
        }
    }

    // --- DATABASE SCRIPTS & RISK METHODS ---
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
            CREATE TABLE IF NOT EXISTS price_history (
                id SERIAL PRIMARY KEY,
                date TEXT NOT NULL,
                commodity TEXT NOT NULL,
                delivery_month TEXT NOT NULL,
                price DOUBLE PRECISION NOT NULL
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

    static List<PriceHistory> GetPriceHistory(NpgsqlConnection connection)
    {
        var historyList = new List<PriceHistory>();
        const string sql = "SELECT id, date, commodity, delivery_month, price FROM price_history ORDER BY id ASC;";

        using var cmd = new NpgsqlCommand(sql, connection);
        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            historyList.Add(new PriceHistory(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetDouble(4)
            ));
        }

        return historyList;
    }

    static double CalculateBucketVar(double netPosition, double currentPrice, double volatility, double zScore, int horizonDays) =>
        Math.Abs(netPosition) * currentPrice * volatility * zScore * Math.Sqrt(horizonDays);

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

    static void WriteNewHistoryTrade(NpgsqlConnection connection, string date, string commodity, string deliveryMonth, double price)
    {
        CreateTable(connection);
        const string sql = """
            INSERT INTO price_history (date, commodity, delivery_month, price) 
            VALUES (@date, @commodity, @delivery_month, @price)
            """;

        using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("date", date);
        cmd.Parameters.AddWithValue("commodity", commodity);
        cmd.Parameters.AddWithValue("delivery_month", deliveryMonth);
        cmd.Parameters.AddWithValue("price", price);

        cmd.ExecuteNonQuery();
        Console.WriteLine("Recorded new price history entry to database.");
    }

    static double CalculateCounterpartyPeakExposure(
        IEnumerable<Trade> trades, 
        string counterparty, 
        Dictionary<(string Commodity, string DeliveryMonth), double> marketPrices, 
        Dictionary<(string Commodity, string DeliveryMonth), double> bucketVolatility)
    {
        double currentMtmExposure = 0.0;
        double totalPfe = 0.0;

        foreach (var trade in trades.Where(t => string.Equals(t.CounterParty, counterparty, StringComparison.OrdinalIgnoreCase)))
        {
            if (!marketPrices.TryGetValue((trade.Commodity, trade.DeliveryMonth), out var marketPrice))
            {
                marketPrice = trade.Price;
            }

            if (!bucketVolatility.TryGetValue((trade.Commodity, trade.DeliveryMonth), out var volatility))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[PFE WARNING] Missing volatility for trade {trade.TradeID} ({trade.Commodity} - {trade.DeliveryMonth}). Defaulting to 2.50% fallback!");
                Console.ResetColor();
                volatility = 0.025;
            }

            bool isBuy = string.Equals(trade.BuySell, "Buy", StringComparison.OrdinalIgnoreCase);

            double tradeMtm = isBuy
                ? trade.Volume * (marketPrice - trade.Price)
                : trade.Volume * (trade.Price - marketPrice);

            currentMtmExposure += tradeMtm;

            double grossNotional = trade.Volume * marketPrice;
            double tradePfe = grossNotional * volatility * 1.645;

            totalPfe += tradePfe;
        }

        double currentCreditRisk = Math.Max(0, currentMtmExposure);
        return currentCreditRisk + totalPfe;
    }

    static void PromptAndWriteNewTrade(
        NpgsqlConnection connection,
        Dictionary<(string Commodity, string DeliveryMonth), double> marketPrices,
        Dictionary<(string Commodity, string DeliveryMonth), double> bucketVolatility,
        Dictionary<string, double> creditLimits)
    {
        Console.WriteLine("\n--- Add New Trade ---");
        Console.Write("Party: ");
        string party = Console.ReadLine() ?? "";
        Console.Write("Counterparty: ");
        string counterparty = Console.ReadLine() ?? "";
        Console.Write("Commodity (Natural Gas/Power): ");
        string commodity = Console.ReadLine() ?? "";
        Console.Write("Volume: ");
        double.TryParse(Console.ReadLine(), out double volume);
        Console.Write("Price: ");
        double.TryParse(Console.ReadLine(), out double price);
        Console.Write("Delivery Month (e.g. August 2-23rd): ");
        string month = Console.ReadLine() ?? "";
        Console.Write("Buy/Sell: ");
        string buySell = Console.ReadLine() ?? "";

        var newTrade = new Trade(0, party, counterparty, commodity, volume, price, month, buySell);

        var existingTrades = GetAllTrades(connection);
        var simulatedTrades = new List<Trade>(existingTrades) { newTrade };

        double newPeakExposure = CalculateCounterpartyPeakExposure(simulatedTrades, counterparty, marketPrices, bucketVolatility);
        double limit = creditLimits.GetValueOrDefault(counterparty, 50000.00);

        if (newPeakExposure > limit)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[BREACH WARNING] Trade rejected!");
            Console.WriteLine($"New Peak Exposure for {counterparty} would be ${newPeakExposure:N2}, exceeding the limit of ${limit:N2}.");
            Console.ResetColor();

            Console.Write("Do you still want to force execute this trade? (Y/N): ");
            string confirm = Console.ReadLine() ?? "";
            if (!confirm.Equals("Y", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Trade entry cancelled.");
                return;
            }
        }
        else if (newPeakExposure > limit * 0.8)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n[LIMIT WARNING] Trade approved, but {counterparty} is at {newPeakExposure / limit * 100:F1}% of credit limit (${newPeakExposure:N2} / ${limit:N2}).");
            Console.ResetColor();
        }

        WriteNewTrade(connection, newTrade);
    }

    static void PromptAndWriteHistoryTrade(NpgsqlConnection connection)
    {
        Console.WriteLine("\n--- Add Price History Entry ---");
        Console.Write("Date (e.g. YYYY-MM-DD): ");
        string date = Console.ReadLine() ?? "";
        Console.Write("Commodity (Natural Gas/Power): ");
        string commodity = Console.ReadLine() ?? "";
        Console.Write("Delivery Month (e.g. August 2-23rd): ");
        string month = Console.ReadLine() ?? "";
        Console.Write("Price: ");
        double.TryParse(Console.ReadLine(), out double price);

        WriteNewHistoryTrade(connection, date, commodity, month, price);
    }

    static void DisplayPriceHistory(NpgsqlConnection connection)
    {
        var history = GetPriceHistory(connection);

        Console.WriteLine("\n=== PRICE HISTORY RECORDS ===");
        if (history.Count == 0)
        {
            Console.WriteLine("No price history records found.");
            return;
        }

        Console.WriteLine($"{"ID",-5} | {"Date",-12} | {"Commodity",-15} | {"Delivery Month",-18} | {"Price",-10}");
        Console.WriteLine(new string('-', 70));

        foreach (var record in history)
        {
            Console.WriteLine($"{record.Id,-5} | {record.Date,-12} | {record.Commodity,-15} | {record.DeliveryMonth,-18} | ${record.Price,-10:F2}");
        }
    }

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

    static void AnalyzeExistingTrades(
        NpgsqlConnection connection, 
        Dictionary<(string Commodity, string DeliveryMonth), double> marketPrices, 
        Dictionary<(string Commodity, string DeliveryMonth), double> bucketVolatility,
        double zScore = 1.645,
        int horizonDays = 1)
    {
        var trades = GetAllTrades(connection);

        Console.WriteLine($"\nLoaded {trades.Count} trades from database:");
        foreach (var trade in trades)
        {
            Console.WriteLine($"Trade ID: {trade.TradeID} | {trade.Commodity} | {trade.DeliveryMonth}");
        }

        var riskBuckets = trades
            .Select(t => (Commodity: t.Commodity, Month: t.DeliveryMonth))
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

            if (!marketPrices.TryGetValue((commodity, month), out var marketPrice))
            {
                Console.WriteLine($"\n[{commodity}] - {month}");
                Console.WriteLine($"   [WARNING] Missing market price for ({commodity}, {month}). Skipping bucket.");
                continue;
            }

            if (!bucketVolatility.TryGetValue((commodity, month), out var dailyVolatility))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"   [VOLATILITY WARNING] Missing measured volatility for ({commodity}, {month}). Defaulting to 2.50% fallback!");
                Console.ResetColor();
                dailyVolatility = 0.025;
            }

            var netPos = CalculateBucketPosition(bucketTrades);
            var bucketPnl = CalculateBucketMtmPnl(bucketTrades, marketPrice, netPos);
            var bucketExposure = CalculateBucketExposure(netPos);
            var bucketVar = CalculateBucketVar(netPos, marketPrice, dailyVolatility, zScore, horizonDays);

            totalPortfolioVar += bucketVar;
            var unit = commodity == "Power" ? "MWh" : "MMBtu";
            string confLabel = Math.Abs(zScore - 2.326) < 0.01 ? "99%" : "95%";

            Console.WriteLine($"\n[{commodity}] - {month}");
            Console.WriteLine($"   Net Position:  {netPos:N0} {unit}");
            Console.WriteLine($"   Volatility:    {dailyVolatility * 100:F3}%");
            Console.WriteLine($"   MTM P&L:       ${bucketPnl:N2}");
            Console.WriteLine($"   Risk Exposure: ${bucketExposure:N2} per 1¢ move");
            Console.WriteLine($"   {horizonDays}-Day {confLabel} VaR: ${bucketVar:N2}");
        }

        Console.WriteLine("\n-----------------------------------------");
        Console.WriteLine($"Total Undiversified Portfolio VaR: ${totalPortfolioVar:N2}");
        Console.WriteLine("-----------------------------------------");
    }

    static void AnalyzeCounterpartyExposure(
        NpgsqlConnection connection, 
        Dictionary<(string Commodity, string DeliveryMonth), double> marketPrices,
        Dictionary<(string Commodity, string DeliveryMonth), double> bucketVolatility,
        Dictionary<string, double> creditLimits)
    {
        var trades = GetAllTrades(connection);

        Console.WriteLine("\n=== COUNTERPARTY CREDIT & PFE REPORT ===");

        var counterpartyGroups = trades.GroupBy(t => t.CounterParty);

        foreach (var group in counterpartyGroups)
        {
            string counterparty = group.Key;
            double currentMtmExposure = 0.0;
            double totalPfe = 0.0;

            foreach (var trade in group)
            {
                if (!marketPrices.TryGetValue((trade.Commodity, trade.DeliveryMonth), out var marketPrice))
                {
                    marketPrice = trade.Price; 
                }

                if (!bucketVolatility.TryGetValue((trade.Commodity, trade.DeliveryMonth), out var volatility))
                {
                    volatility = 0.025;
                }

                bool isBuy = string.Equals(trade.BuySell, "Buy", StringComparison.OrdinalIgnoreCase);
                
                double tradeMtm = isBuy 
                    ? trade.Volume * (marketPrice - trade.Price) 
                    : trade.Volume * (trade.Price - marketPrice);

                currentMtmExposure += tradeMtm;
                
                double grossNotional = trade.Volume * marketPrice;
                double tradePfe = grossNotional * volatility * 1.645;

                totalPfe += tradePfe;
            }
            
            double currentCreditRisk = Math.Max(0, currentMtmExposure);
            double peakExposure = currentCreditRisk + totalPfe;
            double limit = creditLimits.GetValueOrDefault(counterparty, 50000.00);

            string statusFlag = "[OK]";
            if (peakExposure > limit)
            {
                statusFlag = "[BREACH]";
            }
            else if (peakExposure > limit * 0.8)
            {
                statusFlag = "[WARNING]";
            }

            Console.Write($"Counterparty: {counterparty,-15} Status: ");
            if (statusFlag == "[BREACH]")
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(statusFlag);
                Console.ResetColor();
            }
            else if (statusFlag == "[WARNING]")
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine(statusFlag);
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(statusFlag);
                Console.ResetColor();
            }

            Console.WriteLine($"   Credit Limit:        ${limit,12:N2}");
            Console.WriteLine($"   Current MTM Value:   ${currentMtmExposure,12:N2}");
            Console.WriteLine($"   Current Credit Risk: ${currentCreditRisk,12:N2}");
            Console.WriteLine($"   PFE (95% 1-Day):     ${totalPfe,12:N2}");
            Console.WriteLine($"   Total Peak Exposure: ${peakExposure,12:N2}\n");
        }

        Console.WriteLine("-----------------------------------------");
    }

    static void RunStressTestMenu(
        NpgsqlConnection connection,
        Dictionary<(string Commodity, string DeliveryMonth), double> marketPrices,
        Dictionary<(string Commodity, string DeliveryMonth), double> bucketVolatility,
        Dictionary<string, double> creditLimits)
    {
        Console.WriteLine("\n=== STRESS TESTING SCENARIOS ===");
        Console.WriteLine("1. Bull Market (+30% Prices, +50% Volatility)");
        Console.WriteLine("2. Bear Market (-30% Prices, +20% Volatility)");
        Console.WriteLine("3. Volatility Spike Only (2x Volatility)");
        Console.WriteLine("4. Custom Price & Volatility Shock");
        Console.Write("\nSelect scenario (1-4): ");

        var choice = Console.ReadLine();
        double priceMultiplier = 1.0;
        double volMultiplier = 1.0;
        string scenarioName = "";

        switch (choice)
        {
            case "1":
                scenarioName = "Bull Market (+30% Price, +50% Vol)";
                priceMultiplier = 1.30;
                volMultiplier = 1.50;
                break;
            case "2":
                scenarioName = "Bear Market (-30% Price, +20% Vol)";
                priceMultiplier = 0.70;
                volMultiplier = 1.20;
                break;
            case "3":
                scenarioName = "Volatility Spike (2x Vol)";
                priceMultiplier = 1.00;
                volMultiplier = 2.00;
                break;
            case "4":
                Console.Write("Enter Price Change % (e.g. 20 for +20%, -15 for -15%): ");
                double.TryParse(Console.ReadLine(), out double pricePct);
                Console.Write("Enter Volatility Change % (e.g. 50 for +50%): ");
                double.TryParse(Console.ReadLine(), out double volPct);

                scenarioName = $"Custom Shock ({pricePct:+0.0;-0.0}% Price, {volPct:+0.0;-0.0}% Vol)";
                priceMultiplier = 1.0 + (pricePct / 100.0);
                volMultiplier = 1.0 + (volPct / 100.0);
                break;
            default:
                Console.WriteLine("Invalid scenario choice.");
                return;
        }

        var shockedPrices = marketPrices.ToDictionary(k => k.Key, k => k.Value * priceMultiplier);
        var shockedVolatility = bucketVolatility.ToDictionary(k => k.Key, k => k.Value * volMultiplier);

        Console.WriteLine($"\n=========================================");
        Console.WriteLine($" RUNNING SCENARIO: {scenarioName.ToUpper()}");
        Console.WriteLine($"=========================================");

        AnalyzeExistingTrades(connection, shockedPrices, shockedVolatility);
        AnalyzeCounterpartyExposure(connection, shockedPrices, shockedVolatility, creditLimits);
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
}