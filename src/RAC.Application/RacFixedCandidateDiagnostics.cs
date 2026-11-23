using RAC.Domain;

namespace RAC.Application;

/// <summary>
/// Descriptive pre-holdout vs subsequent fixed replay diagnostics.
/// Training vote counts are NOT returns or proof of predictive performance.
/// No promotion, orders, accounts, or operational persistence.
/// </summary>
public static class RacFixedCandidateDiagnostics
{
    public sealed record Row(string Symbol, string FrozenGate, int FrozenHorizonMinutes,
        int TrainingWindows, int FrozenVotes, decimal TrainingVoteSharePct,
        int HoldoutMonths, int HoldoutDays, int HoldoutClosedTrades,
        decimal? MeanHoldoutReturnPct, decimal? MeanDoubleCostReturnPct,
        decimal? DoubleCostDifferencePct, decimal WorstDrawdownPct,
        string DiagnosticStatus, bool EligibleForTrading);

    public sealed record Result(string Mode, int Agents, int HoldoutMonths,
        int NoTradeAgents, int ActiveAgentsWithMeasuredReturn,
        int ActiveAgentsMissingEvidence, bool PaperModified,
        bool OrderPlacementEnabled, bool PromotionAllowed,
        IReadOnlyList<Row> Rows, string Notice);

    public static Result Compare(RacFrozenDecisionAudit.Result frozen,
        IReadOnlyList<RacFixedHoldoutReplay.Result> holdouts)
    {
        if (!frozen.Available || frozen.Agents != RacMarketSymbols.Supported.Length ||
            frozen.PaperModified || frozen.OrderPlacementEnabled ||
            frozen.PromotionAllowed || frozen.Rows.Count != RacMarketSymbols.Supported.Length)
            throw new InvalidDataException("Invalid frozen decision input.");
        var months = RacFrozenDecisionAudit.HoldoutMonths;
        if (holdouts.Count != frozen.Agents * months.Length ||
            holdouts.Any(x => x.PaperModified || x.OrderPlacementEnabled ||
                x.PromotionAllowed || x.ReviewEligible))
            throw new InvalidDataException("Incomplete or unsafe holdout input.");
        var rows = new List<Row>();
        foreach (var symbol in RacMarketSymbols.Supported)
        {
            var frozenPick = frozen.Rows.Single(x => x.Symbol == symbol);
            if (frozenPick.TrainingWindows <= 0 || frozenPick.VotesForFrozen < 1 ||
                frozenPick.VotesForFrozen > frozenPick.TrainingWindows ||
                frozenPick.OutOfSampleWindows != 92)
                throw new InvalidDataException("Invalid pre-holdout votes.");
            RacFrozenDecisionAudit.FreezeByVotes([
                (frozenPick.FrozenEntryGate, frozenPick.FrozenHorizonMinutes)]);
            var items = holdouts.Where(x => x.Symbol == symbol).ToArray();
            if (items.Length != months.Length ||
                months.Any(m => items.Count(x => x.Month == m) != 1) ||
                items.Any(x => x.FrozenGate != frozenPick.FrozenEntryGate ||
                    x.FrozenHorizonMinutes != frozenPick.FrozenHorizonMinutes ||
                    x.TestedDays != DateTime.DaysInMonth(
                        int.Parse(x.Month[..4]), int.Parse(x.Month[5..])) ||
                    x.ClosedTrades < 0 || x.UnresolvedExitIntents < 0 ||
                    x.DoubleCostUnresolvedExitIntents < 0))
                throw new InvalidDataException("Inconsistent fixed holdout input.");
            var noTrade = frozenPick.FrozenEntryGate == "NO_TRADE";
            if (noTrade && items.Any(x => x.ClosedTrades != 0 ||
                x.MeanNetReturnPct != 0m || x.DoubleCostMeanNetReturnPct is not null ||
                x.EvaluationStatus != "NO_TRADE_BASELINE_NOT_ACTIVE_REPLAY"))
                throw new InvalidDataException("NO_TRADE cannot have active performance.");
            var evidence = !noTrade && items.All(x =>
                x.UnresolvedExitIntents == 0 &&
                x.DoubleCostUnresolvedExitIntents == 0 &&
                x.MeanNetReturnPct.HasValue &&
                x.DoubleCostMeanNetReturnPct.HasValue &&
                x.RiskScreenPassed);
            // An unweighted month mean: does not mix monthly mean returns with
            // compounded, realized, or per-trade returns.
            decimal? regular = evidence ? Math.Round(
                items.Average(x => x.MeanNetReturnPct!.Value), 4) : null;
            decimal? stressed = evidence ? Math.Round(
                items.Average(x => x.DoubleCostMeanNetReturnPct!.Value), 4) : null;
            var status = noTrade ? "NO_TRADE_ABSTENTION_BASELINE" :
                evidence ? "ACTIVE_SHADOW_DESCRIPTIVE_ONLY" :
                "ACTIVE_INSUFFICIENT_EVIDENCE";
            rows.Add(new(symbol, frozenPick.FrozenEntryGate,
                frozenPick.FrozenHorizonMinutes, frozenPick.TrainingWindows,
                frozenPick.VotesForFrozen, Math.Round(
                    100m * frozenPick.VotesForFrozen / frozenPick.TrainingWindows, 2),
                items.Length, items.Sum(x => x.TestedDays),
                items.Sum(x => x.ClosedTrades),
                regular, stressed, evidence ? Math.Round(
                    stressed!.Value - regular!.Value, 4) : null,
                items.Max(x => x.WorstDrawdownPct), status, false));
        }
        return new("FROZEN_CANDIDATE_DIAGNOSTICS_SHADOW_ONLY", rows.Count,
            months.Length, rows.Count(x => x.DiagnosticStatus ==
                "NO_TRADE_ABSTENTION_BASELINE"),
            rows.Count(x => x.DiagnosticStatus ==
                "ACTIVE_SHADOW_DESCRIPTIVE_ONLY"),
            rows.Count(x => x.DiagnosticStatus ==
                "ACTIVE_INSUFFICIENT_EVIDENCE"),
            false, false, false, rows,
            "Training vote share measures stability of choices, NOT training returns. " +
            "Holdout figures are hypothetical monthly means, never realized profit. " +
            "No strategy promotion or execution permitted.");
    }
}
