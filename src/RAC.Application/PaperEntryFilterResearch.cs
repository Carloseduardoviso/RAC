using RAC.Domain;

namespace RAC.Application;

/// <summary>
/// Strictly observational candle-response research. Choose a filter using only
/// training candles; report its response on disjoint future holdout candles.
/// A BUY signal on each candle is not an executed trade. No PAPER interaction.
/// </summary>
public static class PaperEntryFilterResearch
{
    public const int MinimumTrainingSignals = 8;
    private const decimal MinimumSmaSeparationPct = 0.05m;
    private static decimal HalfRoundTripCostPct => PaperProfitabilityDiagnostics.RoundTripCostPct / 2m;

    private sealed record Observation(decimal NextCandleNetPct,
        decimal SmaSeparationPct, decimal AtrPct);
    public sealed record Fold(int Number, string SelectedFromTraining,
        int TrainingBaselineSignals, int TrainingFilteredSignals,
        decimal? TrainingBaselineMeanNetPct, decimal? TrainingFilteredMeanNetPct,
        int HoldoutBaselineSignals, int HoldoutSelectedSignals,
        decimal? HoldoutBaselineMeanNetPct, decimal? HoldoutSelectedMeanNetPct,
        int HoldoutSelectedPositiveResponses);
    public sealed record Result(string Symbol, string Status, int CandleCount,
        int FoldCount, int FilteredSelectionFolds, int HoldoutsWithSelectedSignals,
        int TotalBaselineHoldoutSignals, int TotalSelectedHoldoutSignals,
        decimal? WeightedBaselineHoldoutMeanNetPct,
        decimal? WeightedSelectedHoldoutMeanNetPct,
        bool PaperModified, bool OrderPlacementEnabled, bool PromotionAllowed,
        IReadOnlyList<Fold> Folds, string Interpretation);

    public static Result Run(string symbol, IReadOnlyList<MarketCandlePoint> candles,
        DateTimeOffset observedUtc, PaperStrategyWalkForward.Result walk)
    {
        if (!RacMarketSymbols.IsSupported(symbol))
            throw new ArgumentException("Unsupported pair.", nameof(symbol));
        if (candles.Count > PaperStrategyWalkForward.MaxCandles ||
            walk.Symbol != symbol || walk.Status != "COMPLETE") return Blocked(symbol, candles.Count);
        var source = RacStrategyEvaluator.LatestContiguous(candles);
        if (source.Length < PaperStrategyWalkForward.TrainCandles +
                PaperStrategyWalkForward.TestCandles * PaperStrategyWalkForward.MinimumFolds ||
            RacStrategyEvaluator.Evaluate(symbol, source, observedUtc).Status != "READY")
            return Blocked(symbol, source.Length);
        var folds = new List<Fold>();
        for (int testStart = PaperStrategyWalkForward.TrainCandles;
            testStart + PaperStrategyWalkForward.TestCandles <= source.Length;
            testStart += PaperStrategyWalkForward.TestCandles)
        {
            var training = source.Skip(testStart - PaperStrategyWalkForward.TrainCandles)
                .Take(PaperStrategyWalkForward.TrainCandles).ToArray();
            var holdout = source.Skip(testStart).Take(PaperStrategyWalkForward.TestCandles).ToArray();
            var trainObs = Observe(symbol, training);
            var testObs = Observe(symbol, holdout);
            // Only past TRAINING observations select one predeclared candidate.
            // Minimum support prevents one favorable event from selecting a filter.
            var name = "BASELINE";
            var trainSelection = trainObs;
            foreach (var filter in new[] { "TREND_STRENGTH", "ATR_COST", "TREND_AND_ATR" })
            {
                var matching = Apply(trainObs, filter);
                if (matching.Length < MinimumTrainingSignals) continue;
                if (matching.Average(x => x.NextCandleNetPct) >
                    (trainSelection.Length > 0 ? trainSelection.Average(x => x.NextCandleNetPct) : decimal.MinValue))
                {
                    name = filter;
                    trainSelection = matching;
                }
            }
            var selectedHoldout = Apply(testObs, name);
            folds.Add(new Fold(folds.Count + 1, name, trainObs.Length,
                trainSelection.Length, Mean(trainObs), Mean(trainSelection),
                testObs.Length, selectedHoldout.Length, Mean(testObs), Mean(selectedHoldout),
                selectedHoldout.Count(x => x.NextCandleNetPct > 0m)));
        }
        if (folds.Count != walk.FoldCount) return Blocked(symbol, source.Length);
        var allBase = folds.Sum(x => x.HoldoutBaselineSignals);
        var allSelected = folds.Sum(x => x.HoldoutSelectedSignals);
        return new Result(symbol, "COMPLETE", source.Length, folds.Count,
            folds.Count(x => x.SelectedFromTraining != "BASELINE"),
            folds.Count(x => x.HoldoutSelectedSignals > 0), allBase, allSelected,
            Weighted(folds.Select(x => (x.HoldoutBaselineMeanNetPct, x.HoldoutBaselineSignals))),
            Weighted(folds.Select(x => (x.HoldoutSelectedMeanNetPct, x.HoldoutSelectedSignals))),
            false, false, false, folds,
            "Candle signals and next-candle response, NOT filled orders or trading returns. Training-only choice among three predeclared filters plus baseline, using >=8 training observations. Independent chronological holdouts, nonoverlapping tests but overlapping training; holdout signal counts may be zero. Fixed hypothetical costs; no automatic promotion.");
    }

    private static Observation[] Observe(string symbol, MarketCandlePoint[] candles)
    {
        var events = new List<Observation>();
        for (int i = RacStrategyEvaluator.MinimumCandles - 1; i < candles.Length - 1; i++)
        {
            var signal = RacStrategyEvaluator.Evaluate(symbol,
                candles.Take(i + 1).ToArray(), candles[i].CloseUtc);
            if (signal.Status != "READY" || signal.Action != "COMPRA_OBSERVACIONAL" ||
                signal.Sma9 is null || signal.Sma21 is null || signal.Atr14 is null ||
                candles[i].Close <= 0m || candles[i + 1].Open <= 0m) continue;
            var next = candles[i + 1];
            var impact = (PaperProfitabilityDiagnostics.SlippagePctPerSide +
                PaperProfitabilityDiagnostics.HalfSpreadPctPerSide) / 100m;
            var fee = PaperProfitabilityDiagnostics.FeePctPerSide / 100m;
            var entry = next.Open * (1m + impact);
            var exit = next.Close * (1m - impact) * (1m - fee);
            var net = exit / entry * (1m - fee) * 100m - 100m;
            var smaGap = (signal.Sma9.Value - signal.Sma21.Value) / candles[i].Close * 100m;
            var atrPct = signal.Atr14.Value / candles[i].Close * 100m;
            events.Add(new Observation(net, smaGap, atrPct));
        }
        return events.ToArray();
    }

    private static Observation[] Apply(Observation[] events, string name) => name switch
    {
        "BASELINE" => events,
        "TREND_STRENGTH" => events.Where(x => x.SmaSeparationPct >= MinimumSmaSeparationPct).ToArray(),
        "ATR_COST" => events.Where(x => x.AtrPct >= HalfRoundTripCostPct).ToArray(),
        "TREND_AND_ATR" => events.Where(x => x.SmaSeparationPct >= MinimumSmaSeparationPct &&
            x.AtrPct >= HalfRoundTripCostPct).ToArray(),
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private static decimal? Mean(Observation[] events) => events.Length == 0
        ? null : Math.Round(events.Average(x => x.NextCandleNetPct), 4);

    private static decimal? Weighted(IEnumerable<(decimal? Mean, int Count)> samples)
    {
        var valid = samples.Where(x => x.Mean.HasValue && x.Count > 0).ToArray();
        var total = valid.Sum(x => x.Count);
        return total == 0 ? null : Math.Round(valid.Sum(x => x.Mean!.Value * x.Count) / total, 4);
    }

    private static Result Blocked(string symbol, int count) =>
        new(symbol, "INSUFFICIENT_DATA", count, 0, 0, 0, 0, 0,
            null, null, false, false, false, [],
            "Insufficient contiguous recent closed candles or incomplete historical holdouts.");
}
