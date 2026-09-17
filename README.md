# C# .NET ETRM

A small Energy Trade & Risk Management (ETRM) console app in C#. It records commodity trades (natural gas, power) in PostgreSQL, then calculates mark-to-market P&L, net position, risk exposure, and 1-day 95% Value-at-Risk (VaR) per commodity/delivery-month bucket. It also plots net position by delivery month using ScottPlot.

## Features

- Persists trades to a PostgreSQL `trades` table (auto-created on startup)
- Calculates, per commodity/delivery-month bucket:
  - Net position (buy/sell volume netted)
  - Mark-to-market P&L against current market price
  - Risk exposure per 1¢ move
  - 1-day 95% VaR (parametric, using daily volatility and a 1.645 z-score)
- Reports total undiversified portfolio VaR across all buckets
- Generates a line chart (`book_exposure.png`) of net position by delivery month using ScottPlot

## Prerequisites

- .NET SDK
- PostgreSQL instance (local or remote)
- NuGet packages: `Npgsql`, `DotNetEnv`, `ScottPlot`

## Configuration

Create a `.env` file in the project root (or any parent directory — `Env.TraversePath()` searches upward) with your database connection details:

```
DB_HOST=localhost
DB_PORT=5432
DB_NAME=etrm
DB_USER=your_username
DB_PASSWORD=your_password
```

`DB_PORT` defaults to `5432` if not set or invalid.

## Running

```bash
dotnet run
```

On startup the app will:

1. Connect to PostgreSQL and create the `trades` table if it doesn't exist
2. Load and analyze all existing trades, printing a risk & P&L report to the console
3. Save a net-position chart to `book_exposure.png` in the working directory

## Data model

Each row in `trades` represents a single trade:

| Column | Type | Description |
|---|---|---|
| `id` | SERIAL | Primary key |
| `party` | TEXT | Trading party |
| `counterparty` | TEXT | Counterparty |
| `commodity` | TEXT | e.g. `Natural Gas`, `Power` |
| `volume` | DOUBLE PRECISION | Trade volume (MMBtu for gas, MWh for power) |
| `price` | DOUBLE PRECISION | Trade price |
| `delivery_month` | TEXT | Delivery period, e.g. `August 2-23rd` |
| `buy_sell` | TEXT | `Buy` or `Sell` |

Market prices used for MTM/VaR calculations are currently hardcoded in `marketPrices` (in `Program.cs`) as `(Commodity, DeliveryMonth) -> price`.

## Notes / limitations

- Market prices and daily volatility (2.5%) are hardcoded — no live market data feed
- VaR is calculated per bucket and simply summed (undiversified), with no cross-commodity correlation/diversification benefit applied
- The chart currently only plots Natural Gas positions
