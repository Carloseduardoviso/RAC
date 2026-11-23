using RAC.Domain;

namespace RAC.Application;

/// <summary>
/// Two-day bounded historical research. Never edits PAPER or promotes an engine
/// strategy. Train-only horizon selection, disjoint chronological holdouts and
/// explicit evidence-based rejection. Results are NOT a trading recommendation.
/// </summary>
public static class PaperExtendedHistoryResearch
{
    public const int HistoryCandles = 2880;
    public const int TrainCandles = 360;
    public const int HoldoutCandles = 240;
    public const int MinimumFolds = 8;
    public const int MinimumCompletedHoldoutTrades = 12;
    public const decimal MaximumDrawdownPct = 5m;

    public sealed record Fold(int Number, DateTimeOffset TrainStartUtc,
        DateTimeOffset TestStartUtc, DateTimeOffset TestEndUtc,
        int SelectedHorizonMinutes, int TrainSelectedTrades,
        decimal TrainSelectedReturnPct, decimal BaselineTestReturnPct,
        decimal SelectedTestReturnPct, decimal SelectedTestDrawdownPct,
        int SelectedTestTrades, int SelectedTestWins, int SelectedTestLosses,
        int SelectedTestStops);

    public sealed record Result(string Symbol, string Status, string ResearchGate,
        int CandleCount, int CoveredUtcDays, int FoldCount,
        int SelectedTrades, int PositiveHoldouts, int SelectedStops,
        decimal AverageBaselineReturnPct, decimal AverageSelectedReturnPct,
        decimal WorstSelectedDrawdownPct, int TotalSelectedWins,
        int TotalSelectedLosses, bool PaperModified,
        bool OrderPlacementEnabled, bool PromotionAllowed,
        IReadOnlyList<Fold> Folds, string Interpretation);

    public static Result Run(string symbol, IReadOnlyList<MarketCandlePoint> candles,
        DateTimeOffset observedUtc)
    {
        if (!RacMarketSymbols.IsSupported(symbol))
            throw new ArgumentException("Unsupported market symbol.", nameof(symbol));
        if (candles.Count != HistoryCandles) return Blocked(symbol, candles.Count);
        var series = RacStrategyEvaluator.LatestContiguous(candles);
        if (series.Length != HistoryCandles ||
            RacStrategyEvaluator.Evaluate(symbol, series, observedUtc).Status != "READY")
            return Blocked(symbol, series.Length);
        var days = series.Select(x => x.OpenUtc.UtcDateTime.Date).Distinct().Count();
        if (days < 2) return Blocked(symbol, series.Length);
        var folds = new List<Fold>();
        for (var testStart = TrainCandles;
            testStart + HoldoutCandles <= series.Length;
            testStart += HoldoutCandles)
        {
            var train = series.Skip(testStart - TrainCandles)
                .Take(TrainCandles).ToArray();
            var test = series.Skip(testStart).Take(HoldoutCandles).ToArray();
            var candidates = PaperTradeHorizonResearch.Horizons
                .Select(h => PaperTradeHorizonResearch.SimulateSlice(symbol, train, h)).ToArray();
            if (candidates.Any(x => x.Status != "COMPLETE"))
                return Blocked(symbol, series.Length);
            var selected = candidates[0];
            foreach (var c in candidates.Skip(1))
                if (c.CompletedTrades > 0 && c.ReturnPct > selected.ReturnPct)
                    selected = c;
            var baseTest = PaperTradeHorizonResearch.SimulateSlice(symbol, test, 1);
            var selectedTest = selected.HorizonMinutes == 1
                ? baseTest : PaperTradeHorizonResearch.SimulateSlice(symbol, test, selected.HorizonMinutes);
            if (baseTest.Status != "COMPLETE" || selectedTest.Status != "COMPLETE")
                return Blocked(symbol, series.Length);
            folds.Add(new Fold(folds.Count + 1,
                train[0].OpenUtc, test[0].OpenUtc, test[^1].CloseUtc,
                selected.HorizonMinutes, selected.CompletedTrades,
                selected.ReturnPct, baseTest.ReturnPct, selectedTest.ReturnPct,
                selectedTest.MaxDrawdownPct, selectedTest.CompletedTrades,
                selectedTest.WinningTrades, selectedTest.LosingTrades,
                selectedTest.AtrStopExits));
        }
        if (folds.Count < MinimumFolds) return Blocked(symbol, series.Length);
        var avgBase = Math.Round(folds.Average(x => x.BaselineTestReturnPct), 4);
        var avgSelected = Math.Round(folds.Average(x => x.SelectedTestReturnPct), 4);
        var worst = folds.Max(x => x.SelectedTestDrawdownPct);
        var trades = folds.Sum(x => x.SelectedTestTrades);
        var positives = folds.Count(x => x.SelectedTestReturnPct > 0m);
        var insufficient = trades < MinimumCompletedHoldoutTrades;
        var qualifiesForFurtherResearch = !insufficient && positives * 10 >= folds.Count * 7 &&
            avgSelected > 0m && avgSelected > avgBase && worst <= MaximumDrawdownPct;
        return new Result(symbol, "COMPLETE",
            insufficient ? "INSUFFICIENT_TRADES" :
                qualifiesForFurtherResearch ? "RESEARCH_REVIEW_ONLY" : "REJECTED_NO_EDGE",
            series.Length, days, folds.Count, trades, positives,
            folds.Sum(x => x.SelectedTestStops), avgBase, avgSelected,
            worst, folds.Sum(x => x.SelectedTestWins),
            folds.Sum(x => x.SelectedTestLosses), false, false, false, folds,
            "48h limited public data; rolling 360-candle training and separate 240-candle tests. Baseline 1m versus training-selected 1/5/15/30m SHADOW complete trades. Gate requires >=12 closed test trades, >=70% positive tests, average positive and better than baseline, worst drawdown <=5%. Research review only, NEVER automatic PAPER or real execution approval. No-trade benchmark returns 0%. Costs fixed; overlapping training and limited market regimes.");
    }

    private static Result Blocked(string symbol, int n) =>
        new(symbol, "INSUFFICIENT_DATA", "NOT_EVALUATED", n,
            0, 0, 0, 0, 0, 0m, 0m, 0m, 0, 0,
            false, false, false, [],
            "Requires exactly 2,880 fresh contiguous closed 1m candles spanning at least two UTC calendar days.");
}
