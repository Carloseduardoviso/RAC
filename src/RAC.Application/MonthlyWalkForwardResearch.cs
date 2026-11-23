using System.Globalization;
using System.IO.Compression;
using RAC.Domain;

namespace RAC.Application;

/// <summary>
/// Offline month-long complete-trade research. Each UTC day is a self-contained
/// strictly chronological 720m train / 720m holdout, with horizons frozen before
/// the holdout. Full-day regime tags are RETROSPECTIVE, never selection inputs.
/// Nothing in this module can interact with accounts, SQL or exchanges.
/// </summary>
public static class MonthlyWalkForwardResearch
{
    public const int TrainMinutes = 720;
    public const int HoldoutMinutes = 720;
    public const int MinimumClosedTestTrades = 30;
    public const decimal MaximumTestDrawdownPct = 5m;

    public sealed record Day(DateOnly DateUtc, string RetrospectiveRegime,
        int ChosenHorizonMinutes, int TrainingTrades, decimal TrainingReturnPct,
        int TestTrades, int TestWins, int TestLosses, int TestStops,
        decimal BaselineTestReturnPct, decimal SelectedTestReturnPct,
        decimal SelectedTestDrawdownPct, string SelectedEntryGate = "BASELINE",
        int LiquidityBlockedEntries = 0, int PartialEntryFills = 0,
        decimal UnfilledNotionalUsdt = 0m,
        int ExitIntents = 0, int UnresolvedExitIntents = 0,
        int MissingExitLookahead = 0, string ExitCapacityStatus = "NOT_AUDITED");
    public sealed record RegimeSummary(string RetrospectiveRegime,
        int Days, int PositiveTestDays, int ClosedTestTrades,
        decimal MeanSelectedTestReturnPct, decimal WorstTestDrawdownPct);
    public sealed record Result(string Symbol, int Year, int Month, string Status,
        string ResearchGate, int ValidatedCandles, int TestedDays,
        int PositiveTestDays, int CompletedTestTrades, int WinningTestTrades,
        int LosingTestTrades, int TestStops, decimal MeanBaselineTestReturnPct,
        decimal MeanSelectedTestReturnPct, decimal WorstSelectedTestDrawdownPct,
        bool PaperModified, bool OrderPlacementEnabled, bool PromotionAllowed,
        IReadOnlyList<Day> Days, IReadOnlyList<RegimeSummary> Regimes,
        string Interpretation, int UnresolvedExitIntents = 0,
        int ExitIntents = 0);

    public static Result Run(string symbol, int year, int month, Stream archive,
        decimal costMultiplier = 1m, bool entryGateResearch = false,
        bool liquidityStressResearch = false, bool exitCapacityAudit = false)
    {
        if (costMultiplier is not (1m or 2m))
            throw new ArgumentOutOfRangeException(nameof(costMultiplier));
        // The same validation is used as during the import. No unverified
        // archive is allowed into any performance analysis, even from cache.
        var validated = MonthlyArchiveResearch.Analyze(symbol, year, month, archive);
        archive.Position = 0;
        using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);
        using var reader = new StreamReader(zip.Entries.Single().Open());
        var culture = CultureInfo.InvariantCulture;
        var days = new List<Day>(validated.CalendarDays);
        var candles = new List<MarketCandlePoint>(1440);
        DateOnly? date = null;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            var parts = line.Split(','); // Prior full validation guarantees 12 cells.
            var rawOpen = long.Parse(parts[0], culture);
            var openUtc = DateTimeOffset.FromUnixTimeMilliseconds(
                rawOpen > 100_000_000_000_000L ? rawOpen / 1000L : rawOpen);
            var day = DateOnly.FromDateTime(openUtc.UtcDateTime);
            if (date.HasValue && day != date)
            {
                EvaluateDay(date.Value, candles);
                candles.Clear();
            }
            date = day;
            decimal Numeric(int i) => decimal.Parse(parts[i], NumberStyles.AllowDecimalPoint, culture);
            candles.Add(new MarketCandlePoint(symbol, openUtc,
                openUtc.AddMinutes(1).AddMilliseconds(-1),
                Numeric(1), Numeric(2), Numeric(3), Numeric(4), Numeric(5),
                Numeric(7), long.Parse(parts[8], culture)));
        }
        if (date.HasValue) EvaluateDay(date.Value, candles);
        if (days.Count != validated.CalendarDays)
            throw new InvalidDataException("Research days do not match validated archive.");
        var meanBase = Math.Round(days.Average(x => x.BaselineTestReturnPct), 4);
        var meanSelected = Math.Round(days.Average(x => x.SelectedTestReturnPct), 4);
        var worst = days.Max(x => x.SelectedTestDrawdownPct);
        var positive = days.Count(x => x.SelectedTestReturnPct > 0m);
        var tradeCount = days.Sum(x => x.TestTrades);
        var enough = tradeCount >= MinimumClosedTestTrades;
        var reviewOnly = enough && positive * 10 >= days.Count * 7 &&
            meanSelected > 0m && meanSelected > meanBase &&
            worst <= MaximumTestDrawdownPct &&
            (!exitCapacityAudit || days.All(x => x.UnresolvedExitIntents == 0));
        var regimes = days.GroupBy(x => x.RetrospectiveRegime)
            .OrderBy(x => x.Key, StringComparer.Ordinal)
            .Select(g => new RegimeSummary(g.Key, g.Count(),
                g.Count(x => x.SelectedTestReturnPct > 0m),
                g.Sum(x => x.TestTrades),
                Math.Round(g.Average(x => x.SelectedTestReturnPct), 4),
                g.Max(x => x.SelectedTestDrawdownPct))).ToArray();
        return new Result(symbol, year, month, "COMPLETE",
            exitCapacityAudit && days.Any(x => x.UnresolvedExitIntents > 0)
                ? "EXIT_UNRESOLVED_REJECTED" :
            !enough ? "INSUFFICIENT_TRADES" :
                reviewOnly ? "RESEARCH_REVIEW_ONLY" : "REJECTED_NO_EDGE",
            validated.ValidatedCandles, days.Count, positive, tradeCount,
            days.Sum(x => x.TestWins), days.Sum(x => x.TestLosses),
            days.Sum(x => x.TestStops), meanBase, meanSelected, worst,
            false, false, false, days, regimes,
            "Offline monthly holdouts with training-only selection, illustrative costs and optional retrospective 5-candle exit capacity check. Any unresolved or unobservable exit disqualifies research review; hypothetical PnL is NEVER relabeled realized. No-trade comparator 0%. No trading permission.",
            days.Sum(x => x.UnresolvedExitIntents),
            days.Sum(x => x.ExitIntents));

        void EvaluateDay(DateOnly today, List<MarketCandlePoint> data)
        {
            if (data.Count != TrainMinutes + HoldoutMinutes)
                throw new InvalidDataException("Daily time split incomplete.");
            var train = data.Take(TrainMinutes).ToArray();
            var test = data.Skip(TrainMinutes).ToArray();
            // The opt-in 1.3.8 research starts with the literal no-trade
            // alternative (0%): a strategy can only replace it with positive
            // training returns supported by >=3 completed training trades.
            var gateName = entryGateResearch ? "NO_TRADE" : "BASELINE";
            var choice = entryGateResearch
                ? PaperTradeHorizonResearch.SimulateSlice(symbol, train, 1,
                    costMultiplier, "NO_TRADE", liquidityStressResearch)
                : PaperTradeHorizonResearch.SimulateSlice(symbol, train, 1,
                    costMultiplier, "BASELINE", liquidityStressResearch);
            var gates = entryGateResearch
                ? new[] { "BASELINE", "TREND_STRENGTH", "ATR_COST", "TREND_AND_ATR" }
                : new[] { "BASELINE" };
            var horizons = entryGateResearch ? new[] { 15, 30 }
                : PaperTradeHorizonResearch.Horizons;
            foreach (var gate in gates)
            foreach (var horizon in horizons)
            {
                var candidate = PaperTradeHorizonResearch.SimulateSlice(symbol, train,
                    horizon, costMultiplier, gate, liquidityStressResearch);
                if (candidate.Status != "COMPLETE")
                    throw new InvalidDataException("Training simulation blocked.");
                if (entryGateResearch && candidate.CompletedTrades < 3) continue;
                if ((!entryGateResearch || candidate.ReturnPct > 0m) &&
                    candidate.ReturnPct > choice.ReturnPct)
                {
                    gateName = gate;
                    choice = candidate;
                }
            }
            var baseline = PaperTradeHorizonResearch.SimulateSlice(symbol, test, 1,
                costMultiplier, "BASELINE", liquidityStressResearch);
            var selected = !exitCapacityAudit && gateName == "BASELINE" &&
                choice.HorizonMinutes == 1
                ? baseline : PaperTradeHorizonResearch.SimulateSlice(symbol, test,
                    choice.HorizonMinutes, costMultiplier, gateName,
                    liquidityStressResearch, captureExitIntents: exitCapacityAudit);
            if (baseline.Status != "COMPLETE" || selected.Status != "COMPLETE")
                throw new InvalidDataException("Holdout simulation blocked.");
            var regime = validated.Days[days.Count].DescriptiveRegime;
            if (validated.Days[days.Count].DateUtc != today)
                throw new InvalidDataException("Regime date mismatch.");
            var exitCheck = exitCapacityAudit
                ? PaperExitCapacityGate.Assess(selected, test, costMultiplier)
                : null;
            days.Add(new Day(today, regime, choice.HorizonMinutes,
                choice.CompletedTrades, choice.ReturnPct, selected.CompletedTrades,
                selected.WinningTrades, selected.LosingTrades, selected.AtrStopExits,
                baseline.ReturnPct, selected.ReturnPct, selected.MaxDrawdownPct,
                gateName, selected.LiquidityBlockedEntries,
                selected.PartialEntryFills, selected.TotalUnfilledNotional,
                exitCheck?.IntendedExits ?? 0,
                exitCheck?.UnresolvedExits ?? 0,
                exitCheck?.MissingFutureCandles ?? 0,
                exitCheck?.Status ?? "NOT_AUDITED"));
        }
    }
}
