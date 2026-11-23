using RAC.Domain;

namespace RAC.Application;

/// <summary>
/// Research-only robustness screen for three chronological, fixed-choice
/// holdouts. It reports instability; it cannot approve any strategy.
/// </summary>
public static class RacCandidateRobustnessScreen
{
    public sealed record Row(string Symbol, string FrozenGate,
        int EvaluatedMonths, int ClosedTrades, int PositiveMonths,
        decimal? WorstMonthReturnPct, decimal? BestMonthReturnPct,
        decimal? MonthReturnRangePct, decimal? NormalMeanPct,
        decimal? DoubleCostMeanPct, decimal? CostPenaltyPct,
        decimal WorstDrawdownPct, string Status, bool TradingAllowed);

    public sealed record Result(string Mode, int Agents, int AbstainingAgents,
        int InsufficientEvidenceAgents, int UnstableAgents,
        bool PaperModified, bool OrderPlacementEnabled,
        bool PromotionAllowed, IReadOnlyList<Row> Rows, string Notice);

    public static Result Evaluate(RacFrozenDecisionAudit.Result frozen,
        IReadOnlyList<RacFixedHoldoutReplay.Result> holdouts)
    {
        // Reuse the fail-closed inventory and pre-holdout choice validation.
        var diagnostic = RacFixedCandidateDiagnostics.Compare(frozen, holdouts);
        var months = RacFrozenDecisionAudit.HoldoutMonths;
        var rows = new List<Row>();
        foreach (var d in diagnostic.Rows)
        {
            var tests = months.Select(month => holdouts.Single(x =>
                x.Symbol == d.Symbol && x.Month == month)).ToArray();
            var abstention = d.FrozenGate == "NO_TRADE";
            var complete = !abstention && tests.All(x =>
                x.ClosedTrades > 0 && x.UnresolvedExitIntents == 0 &&
                x.DoubleCostUnresolvedExitIntents == 0 &&
                x.MeanNetReturnPct.HasValue &&
                x.DoubleCostMeanNetReturnPct.HasValue &&
                x.RiskScreenPassed &&
                x.EvaluationStatus == "FIXED_SHADOW_REPLAY_RESEARCH_ONLY");
            decimal? minimum = complete ? tests.Min(x => x.MeanNetReturnPct!.Value) : null;
            decimal? maximum = complete ? tests.Max(x => x.MeanNetReturnPct!.Value) : null;
            decimal? normalMean = complete ? Math.Round(
                tests.Average(x => x.MeanNetReturnPct!.Value), 4) : null;
            decimal? stressedMean = complete ? Math.Round(
                tests.Average(x => x.DoubleCostMeanNetReturnPct!.Value), 4) : null;
            decimal? penalty = complete ? Math.Round(
                normalMean!.Value - stressedMean!.Value, 4) : null;
            var positive = complete ? tests.Count(x => x.MeanNetReturnPct > 0m) : 0;
            // Descriptive red flags, never an optimizer or approval rule.
            var unstable = complete && (positive != months.Length ||
                stressedMean <= 0m || minimum <= 0m ||
                penalty < 0m || penalty > normalMean);
            var status = abstention ? "NO_TRADE_NO_ACTIVE_EVIDENCE" :
                !complete ? "ACTIVE_EVIDENCE_INSUFFICIENT" :
                unstable ? "ACTIVE_ROBUSTNESS_WARNING" :
                "ACTIVE_DESCRIPTIVE_ONLY_NOT_APPROVED";
            rows.Add(new(d.Symbol, d.FrozenGate, tests.Length,
                tests.Sum(x => x.ClosedTrades), positive, minimum, maximum,
                complete ? Math.Round(maximum!.Value - minimum!.Value, 4) : null,
                normalMean, stressedMean, penalty,
                tests.Max(x => x.WorstDrawdownPct), status, false));
        }
        return new("FIXED_REPLAY_ROBUSTNESS_SHADOW_ONLY", rows.Count,
            rows.Count(x => x.Status == "NO_TRADE_NO_ACTIVE_EVIDENCE"),
            rows.Count(x => x.Status == "ACTIVE_EVIDENCE_INSUFFICIENT"),
            rows.Count(x => x.Status == "ACTIVE_ROBUSTNESS_WARNING"),
            false, false, false, rows,
            "Three-month sensitivity is descriptive, not statistical proof. " +
            "No active performance is inferred from NO_TRADE, and no trading is permitted.");
    }
}
