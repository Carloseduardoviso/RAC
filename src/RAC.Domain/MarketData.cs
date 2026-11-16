namespace RAC.Domain;

public static class RacMarketSymbols
{
    public const string Source = "BINANCE_SPOT_PUBLIC";
    public const string CandleInterval = "1m";
    public static readonly string[] Supported =
    [
        "BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT",
        "XRPUSDT", "ADAUSDT", "DOGEUSDT", "LINKUSDT",
        "AVAXUSDT", "LTCUSDT", "DOTUSDT", "TRXUSDT"
    ];

    public static bool IsSupported(string? symbol) =>
        symbol is not null && Supported.Contains(symbol, StringComparer.Ordinal);
}

// Public market quotes are intentionally unrelated to simulated positions and P&L.
public sealed class MarketQuote
{
    public required string Symbol { get; set; }
    public decimal PriceUsdt { get; set; }
    public DateTimeOffset ObservedUtc { get; set; }
    public string Source { get; set; } = RacMarketSymbols.Source;
}

public sealed class MarketCandle
{
    public long Id { get; set; }
    public required string Symbol { get; set; }
    public string Interval { get; set; } = RacMarketSymbols.CandleInterval;
    public DateTimeOffset OpenUtc { get; set; }
    public DateTimeOffset CloseUtc { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public decimal Volume { get; set; }
    public decimal QuoteVolume { get; set; }
    public long Trades { get; set; }
    public string Source { get; set; } = RacMarketSymbols.Source;
}

public sealed record MarketPricePoint(string Symbol, decimal PriceUsdt);
public sealed record MarketCandlePoint(
    string Symbol, DateTimeOffset OpenUtc, DateTimeOffset CloseUtc,
    decimal Open, decimal High, decimal Low, decimal Close,
    decimal Volume, decimal QuoteVolume, long Trades);
