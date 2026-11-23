using System.Globalization;
using System.Text.Json;
using RAC.Domain;

namespace RAC.Application;

/// <summary>
/// Pre-August immutable majority-vote decision audit. Uses only May–July
/// daily TRAINING-selected choices to freeze a per-symbol rule. A frozen
/// NO_TRADE has exactly zero paper fills and 0% return in any subsequent
/// period; a non-NO_TRADE winner is marked NEEDS_REPLAY, never assigned the
/// adaptively selected holdout performance. Research-only / read-only.
/// </summary>
public static class RacFrozenDecisionAudit
{
    public static readonly string[] TrainMonths = ["2026-05", "2026-06", "2026-07"];
    public static readonly string[] HoldoutMonths = ["2026-08", "2026-09", "2026-10"];
    public sealed record Pick(string Symbol, string FrozenEntryGate,
        int FrozenHorizonMinutes, int TrainingWindows, int VotesForFrozen,
        int TrainingActiveDecisions, int OutOfSampleWindows,
        int? OutOfSampleSimulatedTrades, decimal? OutOfSampleReturnPct,
        string EvaluationStatus, bool ReviewEligible);
    public sealed record Result(bool Available, string Mode,
        IReadOnlyList<string> TrainingMonths, IReadOnlyList<string> HoldoutMonths,
        IReadOnlyList<string> MissingMonths, int Agents,
        int TrainingWindows, int HoldoutWindows, int FrozenNoTradeAgents,
        int StrategiesNeedingReplay, int AgentsEligibleForReview,
        bool PaperModified, bool OrderPlacementEnabled, bool PromotionAllowed,
        IReadOnlyList<Pick> Rows, string Notice);

    public static (string Gate, int Horizon, int Votes) FreezeByVotes(
        IReadOnlyList<(string Gate, int Horizon)> trainingChoices)
    {
        if (trainingChoices.Count == 0)
            throw new InvalidDataException("No pre-holdout decisions.");
        if (trainingChoices.Any(x => x.Gate is not
                ("NO_TRADE" or "BASELINE" or "TREND_STRENGTH" or "ATR_COST" or
                 "TREND_AND_ATR") || (x.Gate == "NO_TRADE" ?
                    x.Horizon != 1 : x.Horizon is not (15 or 30))))
            throw new InvalidDataException("Unsupported frozen decision.");
        var winner = trainingChoices.GroupBy(x => x)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key.Gate == "NO_TRADE" ? 0 : 1)
            .ThenBy(x => x.Key.Gate, StringComparer.Ordinal)
            .ThenBy(x => x.Key.Horizon).First();
        return (winner.Key.Gate, winner.Key.Horizon, winner.Count());
    }

    public static Result Load(string monthlyRoot)
    {
        // Verify all six independent monthly projections first. Missing data
        // is never substituted with zero return.
        var months = TrainMonths.Concat(HoldoutMonths).ToArray();
        var projections = months.ToDictionary(m => m,
            m => RacShadowDashboardReport.Load(monthlyRoot, m));
        var missing = months.Where(m => !projections[m].Available).ToArray();
        if (missing.Length > 0)
            return new(false, "FROZEN_DECISION_INCOMPLETE_SHADOW_ONLY",
                TrainMonths, HoldoutMonths, missing, 0, 0, 0, 0, 0, 0,
                false, false, false, [],
                "Histórico incompleto. Nenhum resultado fora da amostra foi inferido.");
        if (projections.Values.Any(p => p.PaperModified ||
            p.OrderPlacementEnabled || p.PromotionAllowed || p.Agents != 12))
            throw new InvalidDataException("Unsafe monthly input.");
        var rows = new List<Pick>();
        foreach (var symbol in RacMarketSymbols.Supported)
        {
            var votes = new List<(string Gate, int Horizon)>();
            foreach (var month in TrainMonths)
            {
                var path = Path.Combine(monthlyRoot, month,
                    "exit-capacity-gated-backtest.json");
                using var json = JsonDocument.Parse(File.ReadAllText(path),
                    new JsonDocumentOptions { MaxDepth = 20 });
                var a = json.RootElement.GetProperty("agents")
                    .EnumerateArray().Single(x =>
                        x.GetProperty("symbol").GetString() == symbol);
                var selection = a.GetProperty("selection").EnumerateArray().ToArray();
                var expected = DateTime.DaysInMonth(int.Parse(month[..4],
                    CultureInfo.InvariantCulture), int.Parse(month[5..],
                    CultureInfo.InvariantCulture));
                if (selection.Length != expected)
                    throw new InvalidDataException("Incomplete pre-holdout training decisions.");
                for (var i = 0; i < selection.Length; i++)
                {
                    var entry = selection[i];
                    var date = DateOnly.Parse(entry.GetProperty("DateUtc").GetString()!,
                        CultureInfo.InvariantCulture);
                    if (date != new DateOnly(int.Parse(month[..4],
                        CultureInfo.InvariantCulture), int.Parse(month[5..],
                        CultureInfo.InvariantCulture), i + 1))
                        throw new InvalidDataException("Nonchronological training decision.");
                    var gate = entry.GetProperty("SelectedEntryGate").GetString()!;
                    var horizon = entry.GetProperty("ChosenHorizonMinutes").GetInt32();
                    if (gate is not ("NO_TRADE" or "BASELINE" or "TREND_STRENGTH" or
                        "ATR_COST" or "TREND_AND_ATR") ||
                        (gate == "NO_TRADE" ? horizon != 1 : horizon is not (15 or 30)))
                        throw new InvalidDataException("Unexpected training selection.");
                    votes.Add((gate, horizon));
                }
            }
            var winner = FreezeByVotes(votes);
            var noTrade = winner.Gate == "NO_TRADE";
            var holdoutWindows = HoldoutMonths.Sum(m =>
                projections[m].Rows.Single(x => x.Symbol == symbol).TotalDays);
            rows.Add(new(symbol, winner.Gate, winner.Horizon,
                votes.Count, winner.Votes, votes.Count(x => x.Gate != "NO_TRADE"),
                holdoutWindows, noTrade ? 0 : null, noTrade ? 0m : null,
                noTrade ? "FROZEN_NO_TRADE_ZERO_EXECUTIONS" :
                    "FIXED_STRATEGY_REPLAY_REQUIRED", false));
        }
        if (rows.Select(x => x.Symbol).Distinct(StringComparer.Ordinal).Count() != 12)
            throw new InvalidDataException("Duplicate study agents.");
        return new(true, "PRE_HOLDOUT_FROZEN_DECISION_SHADOW_ONLY",
            TrainMonths, HoldoutMonths, [], rows.Count,
            rows.Sum(x => x.TrainingWindows), rows.Sum(x => x.OutOfSampleWindows),
            rows.Count(x => x.FrozenEntryGate == "NO_TRADE"),
            rows.Count(x => x.EvaluationStatus == "FIXED_STRATEGY_REPLAY_REQUIRED"),
            0, false, false, false, rows,
            "Majority decision frozen from May–July ONLY. August–October does not choose the gate. " +
            "NO_TRADE means no orders and exactly 0% simulated return; NOT profit. " +
            "Non-NO_TRADE decisions require genuine fixed-parameter replay and cannot borrow adaptive backtest returns. " +
            "This is an abstention baseline, not proof of a profitable strategy. No execution authorized.");
    }
}
