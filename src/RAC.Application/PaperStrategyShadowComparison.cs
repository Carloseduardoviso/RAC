using RAC.Domain;

namespace RAC.Application;

/// <summary>
/// A completely isolated historical experiment. No database writes or order executors.
/// Both candidates use exactly the same deterministic signal evaluator, position
/// sizing and hypothetical costs; the only difference is a five-candle reentry
/// cooldown after an exit. Stop is frozen from ATR at entry, 2x ATR below fill.
/// An intrabar stop is filled at the stop or a worse opening gap, never better.
/// </summary>
public static class PaperStrategyShadowComparison
{
    public const int CooldownCandles = 5;
    private const decimal Fee = 0.1m;
    private const decimal Slippage = 0.05m;
    private const decimal HalfSpread = 0.01m;

    public sealed record Slice(string Status, int Candles, decimal ReturnPct,
        decimal MaxDrawdownPct, int CompletedTrades, int AtrStops,
        int SignalExits, int WinningTrades, int LosingTrades, int SkippedReentries,
        string? Note);

    public sealed record Comparison(string Symbol, string StrategyVersion,
        string Status, int CandleCount, DateTimeOffset? FirstUtc,
        DateTimeOffset? LastUtc, Slice EarlierBaseline, Slice EarlierCooldown,
        Slice LaterBaseline, Slice LaterCooldown, bool PromotionAllowed,
        string Interpretation);

    public static Comparison Run(string symbol, IReadOnlyList<MarketCandlePoint> data,
        DateTimeOffset observedUtc)
    {
        if (!RacMarketSymbols.IsSupported(symbol))
            throw new ArgumentException("Unsupported monitored pair.", nameof(symbol));
        var source = RacStrategyEvaluator.LatestContiguous(data);
        var valid = source.Length >= 2 * (RacStrategyEvaluator.MinimumCandles + 10)
            && source.Length <= 2000
            && RacStrategyEvaluator.Evaluate(symbol, source, observedUtc).Status == "READY";
        if (!valid)
        {
            var empty = Unavailable(source.Length);
            return new(symbol, RacStrategyEvaluator.Version, "INSUFFICIENT_DATA",
                source.Length, source.FirstOrDefault()?.OpenUtc,
                source.LastOrDefault()?.CloseUtc, empty, empty, empty, empty, false,
                "Insufficient fresh contiguous closed candles for two independent windows.");
        }
        // Time-ordered, disjoint halves. Indicators are warmed up inside each
        // half and neither run can see future candles or the other half.
        var middle = source.Length / 2;
        var earlier = source.Take(middle).ToArray();
        var later = source.Skip(middle).ToArray();
        return new(symbol, RacStrategyEvaluator.Version, "COMPLETE",
            source.Length, source[0].OpenUtc, source[^1].CloseUtc,
            Simulate(symbol, earlier, 0), Simulate(symbol, earlier, CooldownCandles),
            Simulate(symbol, later, 0), Simulate(symbol, later, CooldownCandles),
            false,
            "Historical time split, NOT independently validated out-of-sample optimization. No strategy or PAPER changes. Stop/cooldown, all-in 100 USDT and fixed cost assumptions differ from BRL PAPER. Do not promote automatically.");
    }

    private static Slice Unavailable(int candles) =>
        new("BLOCKED", candles, 0m, 0m, 0, 0, 0, 0, 0, 0,
            "No results when history is insufficient or stale.");

    private static Slice Simulate(string symbol, MarketCandlePoint[] candles, int cooldown)
    {
        decimal cash = 100m, qty = 0m, stop = 0m, entryValue = 0m;
        decimal peak = 100m, maxDd = 0m;
        int sells = 0, atrStops = 0, signalExits = 0, wins = 0, losses = 0;
        int skipped = 0, lastExit = -100000;
        for (var i = RacStrategyEvaluator.MinimumCandles - 1; i < candles.Length - 1; i++)
        {
            var signal = RacStrategyEvaluator.Evaluate(symbol,
                candles.Take(i + 1).ToArray(), candles[i].CloseUtc);
            var next = candles[i + 1];
            if (signal.Status != "READY") continue;
            // Signal-based exit occurs on the next candle OPEN, before
            // possible stop processing. No fills at the signaling candle close.
            if (qty > 0m && signal.Action == "SAIDA_OBSERVACIONAL")
            {
                cash = Sell(qty, next.Open);
                CompleteExit(cash, entryValue, ref sells, ref wins, ref losses);
                qty = 0m; signalExits++; lastExit = i + 1;
            }
            if (qty == 0m && signal.Action == "COMPRA_OBSERVACIONAL")
            {
                if (i + 1 - lastExit <= cooldown) skipped++;
                else if (signal.Atr14 is > 0m && next.Open > 0m)
                {
                    entryValue = cash;
                    var entryPrice = next.Open * (1m + (Slippage + HalfSpread) / 100m);
                    qty = cash * (1m - Fee / 100m) / entryPrice;
                    cash = 0m;
                    stop = Math.Max(0m, entryPrice - 2m * signal.Atr14.Value);
                }
            }
            // Conservative gap handling: cannot sell at a stop higher than
            // the next opening quote after the price gaps below that stop.
            if (qty > 0m && next.Low <= stop)
            {
                cash = Sell(qty, Math.Min(next.Open, stop));
                CompleteExit(cash, entryValue, ref sells, ref wins, ref losses);
                qty = 0m; atrStops++; lastExit = i + 1;
            }
            var equity = cash + qty * next.Close;
            peak = Math.Max(peak, equity);
            if (peak > 0m) maxDd = Math.Max(maxDd,
                (peak - equity) / peak * 100m);
        }
        var finalEquity = cash + (qty > 0m ? Sell(qty, candles[^1].Close) : 0m);
        peak = Math.Max(peak, finalEquity);
        if (peak > 0m) maxDd = Math.Max(maxDd,
            (peak - finalEquity) / peak * 100m);
        return new("COMPLETE", candles.Length,
            Math.Round(finalEquity - 100m, 4), Math.Round(maxDd, 4),
            sells, atrStops, signalExits, wins, losses, skipped, null);
    }

    private static decimal Sell(decimal qty, decimal price) =>
        qty * Math.Max(0m, price) * (1m - (Slippage + HalfSpread) / 100m)
            * (1m - Fee / 100m);

    private static void CompleteExit(decimal cash, decimal entry,
        ref int sells, ref int wins, ref int losses)
    {
        sells++;
        if (cash > entry) wins++;
        else if (cash < entry) losses++;
    }
}
