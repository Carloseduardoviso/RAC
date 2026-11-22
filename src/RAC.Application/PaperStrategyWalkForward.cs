using RAC.Domain;

namespace RAC.Application;

/// <summary>
/// Strict chronological evaluation: select the only candidate from past training
/// data, lock that selection, evaluate it on the immediately following holdout.
/// Folds may overlap in their TRAINING windows but holdouts NEVER overlap.
/// No trading logic is modified and no candidate may be promoted automatically.
/// </summary>
public static class PaperStrategyWalkForward
{
    public const int TrainCandles = 180;
    public const int TestCandles = 120;
    public const int MinimumFolds = 2;
    public const int MaxCandles = 1000;

    public sealed record Fold(int Number, DateTimeOffset TrainStartUtc,
        DateTimeOffset TrainEndUtc, DateTimeOffset TestStartUtc,
        DateTimeOffset TestEndUtc, string SelectedFromTraining,
        decimal TrainingBasePct, decimal TrainingCooldownPct,
        decimal HoldoutBaselinePct, decimal HoldoutSelectedPct,
        decimal HoldoutCandidatePct, int HoldoutBaselineStops,
        int HoldoutSelectedStops, int HoldoutSelectedTrades);

    public sealed record Result(string Symbol, string Status, int CandleCount,
        DateTimeOffset? FirstUtc, DateTimeOffset? LastUtc, int FoldCount,
        int SelectedBaselineFolds, int SelectedCooldownFolds,
        int PositiveSelectedHoldouts, decimal AverageHoldoutBaselinePct,
        decimal AverageHoldoutSelectedPct, bool PromotionAllowed,
        IReadOnlyList<Fold> Folds, string Interpretation);

    public static Result Run(string symbol, IReadOnlyList<MarketCandlePoint> data,
        DateTimeOffset observedUtc)
    {
        if (!RacMarketSymbols.IsSupported(symbol))
            throw new ArgumentException("Unsupported monitored pair.", nameof(symbol));
        // Max size check blocks accidental unbounded work; never silently
        // trim the observed prefix to manufacture a favorable test period.
        if (data.Count > MaxCandles) return Blocked(symbol, data.Count);
        var series = RacStrategyEvaluator.LatestContiguous(data);
        if (series.Length < TrainCandles + TestCandles * MinimumFolds ||
            RacStrategyEvaluator.Evaluate(symbol, series, observedUtc).Status != "READY")
            return Blocked(symbol, series.Length);

        // Earliest possible fixed training block, then non-overlapping later
        // test windows, stepping forward by one entire holdout each time.
        var folds = new List<Fold>();
        for (int testStart = TrainCandles;
             testStart + TestCandles <= series.Length;
             testStart += TestCandles)
        {
            var training = series.Skip(testStart - TrainCandles)
                .Take(TrainCandles).ToArray();
            var holdout = series.Skip(testStart).Take(TestCandles).ToArray();
            var trainBase = PaperStrategyShadowComparison.SimulateSlice(symbol, training, 0);
            var trainPause = PaperStrategyShadowComparison.SimulateSlice(symbol, training,
                PaperStrategyShadowComparison.CooldownCandles);
            var testBase = PaperStrategyShadowComparison.SimulateSlice(symbol, holdout, 0);
            var testPause = PaperStrategyShadowComparison.SimulateSlice(symbol, holdout,
                PaperStrategyShadowComparison.CooldownCandles);
            if (trainBase.Status != "COMPLETE" || trainPause.Status != "COMPLETE" ||
                testBase.Status != "COMPLETE" || testPause.Status != "COMPLETE")
                return Blocked(symbol, series.Length);

            // Deterministic tie rule: keep baseline. The holdout cannot
            // influence this choice because selection uses TRAINING only.
            var useCooldown = trainPause.ReturnPct > trainBase.ReturnPct;
            var selected = useCooldown ? testPause : testBase;
            folds.Add(new Fold(folds.Count + 1,
                training[0].OpenUtc, training[^1].CloseUtc,
                holdout[0].OpenUtc, holdout[^1].CloseUtc,
                useCooldown ? "COOLDOWN_5" : "BASELINE",
                trainBase.ReturnPct, trainPause.ReturnPct,
                testBase.ReturnPct, selected.ReturnPct, testPause.ReturnPct,
                testBase.AtrStops, selected.AtrStops, selected.CompletedTrades));
        }
        if (folds.Count < MinimumFolds) return Blocked(symbol, series.Length);
        return new Result(symbol, "COMPLETE", series.Length,
            series[0].OpenUtc, series[^1].CloseUtc, folds.Count,
            folds.Count(x => x.SelectedFromTraining == "BASELINE"),
            folds.Count(x => x.SelectedFromTraining == "COOLDOWN_5"),
            folds.Count(x => x.HoldoutSelectedPct > 0m),
            Math.Round(folds.Average(x => x.HoldoutBaselinePct), 4),
            Math.Round(folds.Average(x => x.HoldoutSelectedPct), 4),
            false, folds,
            "Train-only selection followed by disjoint chronological test windows. Historical exploratory analysis, not proven live profitability. Training windows overlap across folds. Each holdout resets to 100 USDT. No order execution, no strategy promotion or PAPER mutation.");
    }

    private static Result Blocked(string symbol, int count) =>
        new(symbol, "INSUFFICIENT_DATA", count, null, null, 0,
            0, 0, 0, 0m, 0m, false, [],
            "Requires >=420 recent contiguous 1m closed candles and a non-stale final candle. No synthetic results created.");
}
