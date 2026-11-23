namespace RAC.Application;

/// <summary>
/// Conservative descriptive screen for data-snooping/overfitting concerns.
/// A three-month sign check is not a confidence interval for future returns;
/// month independence and fixed strategy selection are NOT established.
/// </summary>
public static class RacShadowOverfitDiagnostics
{
    public const int MinimumNonzeroMonthsForScreen = 6;
    public const decimal FamilyWiseAlpha = 0.05m;
    public sealed record Agent(string Symbol, int MonthsObserved,
        int PositiveMonths, int NegativeMonths, int FlatMonths,
        int ActiveMonths, int HypotheticalTrades, int UnresolvedExits,
        decimal BestMonthlyMeanPct, decimal WorstMonthlyMeanPct,
        decimal OneSidedSignP, decimal BonferroniAdjustedP,
        string EvidenceStatus, bool EligibleForResearchReview);
    public sealed record Result(bool Available, string Mode,
        int MonthsObserved, int Agents, int AgentsWithPositiveEveryMonth,
        int AgentsWithUnresolvedExits, int AgentsStatisticallySupported,
        int AgentsEligibleForResearchReview, decimal Alpha,
        int MultipleComparisons, int MinimumNonzeroMonths,
        bool PaperModified, bool OrderPlacementEnabled, bool PromotionAllowed,
        IReadOnlyList<Agent> Rows, string Notice);

    public static Result Assess(RacShadowThreeMonthDashboard.Result history)
    {
        if (!history.Available)
            return new(false, "OVERFIT_SCREEN_INCOMPLETE_SHADOW_ONLY", 0,
                0, 0, 0, 0, 0, FamilyWiseAlpha, 12,
                MinimumNonzeroMonthsForScreen, false, false, false, [],
                "Histórico incompleto: nenhuma evidência estatística calculada.");
        if (history.Mode != "THREE_MONTH_EXIT_CAPACITY_SHADOW_ONLY" ||
            history.PaperModified || history.OrderPlacementEnabled ||
            history.PromotionAllowed || history.AgentCount != 12 ||
            history.Rows.Count != 12 || history.Timeline.Count != 3 ||
            history.RequiredMonths.Count != 3 || history.MissingMonths.Count != 0)
            throw new InvalidDataException("Invalid or unsafe SHADOW input.");
        var rows = history.Rows.Select(a =>
        {
            var ordered = a.Months;
            if (ordered.Count != 3 ||
                !ordered.Select(x => x.MonthKey).SequenceEqual(
                    RacShadowThreeMonthDashboard.Months) ||
                ordered.Any(x => x.HypotheticalTrades < 0 ||
                    x.UnresolvedExits < 0 || x.NoTradeDays < 0))
                throw new InvalidDataException("Invalid three-month agent history.");
            var positives = ordered.Count(x => x.HypotheticalMeanReturnPct > 0m);
            var negatives = ordered.Count(x => x.HypotheticalMeanReturnPct < 0m);
            var flat = ordered.Count - positives - negatives;
            var active = ordered.Count(x => x.HypotheticalTrades > 0);
            var trials = positives + negatives;
            var p = OneSidedSignTest(positives, trials);
            var adjusted = Math.Min(1m, p * history.AgentCount);
            // This is only a screen: never substitute a statistical p value
            // for execution approval. A minimum of six nonzero months is required.
            var status = a.UnresolvedExits > 0 ? "UNRESOLVED_EXIT_RISK" :
                active == 0 ? "NO_TRADES" :
                negatives > 0 ? "NEGATIVE_MONTHS" :
                trials < MinimumNonzeroMonthsForScreen ? "INSUFFICIENT_MONTHS" :
                adjusted >= FamilyWiseAlpha ? "NOT_SIGNIFICANT_AFTER_12_TESTS" :
                "RESEARCH_ONLY_REVIEW";
            var researchEligible = status == "RESEARCH_ONLY_REVIEW" &&
                a.AllMonthsResearchReviewEligible;
            return new Agent(a.Symbol, ordered.Count, positives, negatives,
                flat, active, a.HypotheticalTrades, a.UnresolvedExits,
                ordered.Max(x => x.HypotheticalMeanReturnPct),
                ordered.Min(x => x.HypotheticalMeanReturnPct), p, adjusted,
                status, researchEligible);
        }).ToArray();
        if (rows.Select(x => x.Symbol).Distinct(StringComparer.Ordinal).Count() != 12 ||
            rows.Sum(x => x.HypotheticalTrades) != history.HypotheticalTrades ||
            rows.Sum(x => x.UnresolvedExits) != history.UnresolvedExits)
            throw new InvalidDataException("Unreconciled SHADOW study totals.");
        return new(true, "THREE_MONTH_OVERFIT_SCREEN_SHADOW_ONLY", 3, 12,
            rows.Count(x => x.PositiveMonths == 3),
            rows.Count(x => x.UnresolvedExits > 0),
            rows.Count(x => x.BonferroniAdjustedP < FamilyWiseAlpha &&
                x.PositiveMonths + x.NegativeMonths >= MinimumNonzeroMonthsForScreen),
            rows.Count(x => x.EligibleForResearchReview), FamilyWiseAlpha,
            12, MinimumNonzeroMonthsForScreen, false, false, false,
            rows, "Teste unilateral de sinais sobre médias mensais não nulas, " +
            "com ajuste Bonferroni para 12 comparações. Três meses são " +
            "INSUFICIENTES para evidência robusta (mínimo exploratório de 6). " +
            "Independência dos meses, custos reais e ausência de overfitting " +
            "NÃO foram demonstradas. p-valor não é probabilidade de lucro futuro. " +
            "Nenhuma ordem ou aprovação operacional.");
    }

    public static decimal OneSidedSignTest(int positives, int nonzeroMonths)
    {
        if (nonzeroMonths < 0 || nonzeroMonths > 60 ||
            positives < 0 || positives > nonzeroMonths)
            throw new ArgumentOutOfRangeException(nameof(positives));
        if (nonzeroMonths == 0) return 1m;
        // Sum_{k=positives..n} C(n,k) / 2^n.
        // Decimal arithmetic remains exact for n<=60.
        decimal combination = 1m;
        decimal tail = 0m;
        for (var k = 0; k <= nonzeroMonths; k++)
        {
            if (k >= positives) tail += combination;
            if (k < nonzeroMonths)
                combination = combination * (nonzeroMonths - k) / (k + 1);
        }
        decimal denominator = 1m;
        for (var i = 0; i < nonzeroMonths; i++) denominator *= 2m;
        return Math.Min(1m, tail / denominator);
    }
}
