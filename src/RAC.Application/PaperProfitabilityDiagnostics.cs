using RAC.Domain;

namespace RAC.Application;

/// <summary>
/// Read-only research diagnostics. One-candle moves after BUY signals are NOT
/// filled round-trip trades or proof of a trading edge. All estimates are
/// explicitly hypothetical; never write to PAPER or place orders.
/// </summary>
public static class PaperProfitabilityDiagnostics
{
    public const decimal FeePctPerSide = 0.1m;
    public const decimal SlippagePctPerSide = 0.05m;
    public const decimal HalfSpreadPctPerSide = 0.01m;
    public static decimal RoundTripCostPct =>
        100m * (1m - (1m - (FeePctPerSide + SlippagePctPerSide + HalfSpreadPctPerSide) / 100m)
            / (1m + (SlippagePctPerSide + HalfSpreadPctPerSide) / 100m)
            * (1m - FeePctPerSide / 100m));

    public sealed record Result(string Symbol, string Status, int CandleCount,
        int BuySignalCandles, int SellSignalCandles, int BuyWithNextCandle,
        int PositiveNextCandleMoves, int NextCandleMovesAboveRoundTripCost,
        decimal? MeanNextCandleGrossMovePct, decimal? MeanNextCandleNetIfRoundTripPct,
        int LowAtrBuySignals, int HighAtrBuySignals, decimal RoundTripCostPct,
        int HistoricalHoldoutStopsBaseline, int HistoricalHoldoutStopsSelected,
        int HistoricalHoldoutSelectedTrades, int HistoricalHoldouts,
        bool PaperModified, bool OrderPlacementEnabled, string Interpretation);

    public static Result Run(string symbol, IReadOnlyList<MarketCandlePoint> input,
        DateTimeOffset observedUtc, PaperStrategyWalkForward.Result walkForward)
    {
        if (!RacMarketSymbols.IsSupported(symbol))
            throw new ArgumentException("Unsupported monitored pair.", nameof(symbol));
        if (input.Count > PaperStrategyWalkForward.MaxCandles ||
            walkForward.Symbol != symbol || walkForward.Status != "COMPLETE")
            return Blocked(symbol, input.Count);
        var data = RacStrategyEvaluator.LatestContiguous(input);
        if (data.Length < PaperStrategyWalkForward.TrainCandles +
                PaperStrategyWalkForward.TestCandles * PaperStrategyWalkForward.MinimumFolds ||
            RacStrategyEvaluator.Evaluate(symbol, data, observedUtc).Status != "READY")
            return Blocked(symbol, data.Length);
        int buys = 0, sells = 0, samples = 0, positive = 0, aboveCost = 0;
        int lowAtr = 0, highAtr = 0;
        decimal moveTotal = 0m, netTotal = 0m;
        var cost = RoundTripCostPct;
        for (int i = RacStrategyEvaluator.MinimumCandles - 1; i < data.Length - 1; i++)
        {
            var context = data.Take(i + 1).ToArray();
            var signal = RacStrategyEvaluator.Evaluate(symbol, context, data[i].CloseUtc);
            if (signal.Status != "READY") return Blocked(symbol, data.Length);
            if (signal.Action == "SAIDA_OBSERVACIONAL") sells++;
            if (signal.Action != "COMPRA_OBSERVACIONAL") continue;
            buys++;
            var next = data[i + 1];
            if (next.Open <= 0m || signal.Atr14 is not > 0m) continue;
            samples++;
            var gross = (next.Close - next.Open) / next.Open * 100m;
            // Hypothetical next-candle round trip, not the PAPER execution.
            var entry = next.Open * (1m + (SlippagePctPerSide + HalfSpreadPctPerSide) / 100m);
            var buyFee = FeePctPerSide / 100m;
            var exit = next.Close * (1m - (SlippagePctPerSide + HalfSpreadPctPerSide) / 100m)
                * (1m - FeePctPerSide / 100m);
            var net = exit / entry * (1m - buyFee) * 100m - 100m;
            moveTotal += gross;
            netTotal += net;
            if (gross > 0m) positive++;
            if (net > 0m) aboveCost++;
            var atrPct = signal.Atr14.Value / data[i].Close * 100m;
            if (atrPct < cost) lowAtr++; else highAtr++;
        }
        return new(symbol, "COMPLETE", data.Length, buys, sells, samples,
            positive, aboveCost,
            samples > 0 ? Math.Round(moveTotal / samples, 4) : null,
            samples > 0 ? Math.Round(netTotal / samples, 4) : null,
            lowAtr, highAtr, Math.Round(cost, 4),
            walkForward.Folds.Sum(x => x.HoldoutBaselineStops),
            walkForward.Folds.Sum(x => x.HoldoutSelectedStops),
            walkForward.Folds.Sum(x => x.HoldoutSelectedTrades),
            walkForward.FoldCount, false, false,
            "BUY candle count is NOT trades. Next-candle gross and cost-adjusted changes are isolated signal diagnostics, not PAPER fills, not a realized PnL series. ATR regime compares ATR/close with assumed round-trip cost. Stops are from separate hypothetical walk-forward holdouts. No automatic promotion.");
    }

    private static Result Blocked(string symbol, int count) =>
        new(symbol, "INSUFFICIENT_DATA", count, 0, 0, 0, 0, 0,
            null, null, 0, 0, Math.Round(RoundTripCostPct, 4),
            0, 0, 0, 0, false, false,
            "Unavailable without recent contiguous candles and completed walk-forward holdouts.");
}
