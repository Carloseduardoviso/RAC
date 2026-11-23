using RAC.Domain;

namespace RAC.Application;

/// <summary>Chronological six-month SHADOW evaluation. Months are not training
/// inputs for other months; monthly papers use their own earlier 720-minute
/// training and next 720-minute holdout. This is descriptive, not a proof
/// of independent or prospective execution results.</summary>
public static class RacShadowSixMonthValidation
{
    public static readonly string[] RequiredMonths =
        ["2026-05", "2026-06", "2026-07", "2026-08", "2026-09", "2026-10"];

    public sealed record Month(string MonthKey, int AgentDays, int NoTradeDays,
        int HypotheticalTrades, int UnresolvedExits,
        int AgentsRejected, decimal MeanHypotheticalReturnPct);
    public sealed record Agent(string Symbol, int PositiveMonths,
        int NegativeMonths, int FlatMonths, int ActiveMonths,
        int HypotheticalTrades, int UnresolvedExits,
        decimal OneSidedSignP, decimal AdjustedP,
        decimal WorstMonthlyMeanPct, string EvidenceStatus,
        bool EligibleForResearchReview);
    public sealed record Result(bool Available, string Mode,
        IReadOnlyList<string> RequiredMonths,
        IReadOnlyList<string> MissingMonths, int Agents,
        int AgentDays, int NoTradeDays, int HypotheticalTrades,
        int UnresolvedExits, int AgentsWithUnresolvedExits,
        int StatisticallySupportedAgents,
        int AgentsEligibleForResearchReview,
        bool PaperModified, bool OrderPlacementEnabled,
        bool PromotionAllowed, IReadOnlyList<Month> Timeline,
        IReadOnlyList<Agent> Rows, string Notice);

    public static Result Load(string monthlyRoot)
    {
        var monthly = RequiredMonths.Select(m =>
            RacShadowDashboardReport.Load(monthlyRoot, m)).ToArray();
        var missing = monthly.Where(m => !m.Available)
            .Select(m => m.Month).ToArray();
        if (missing.Length > 0)
            return new(false, "SIX_MONTH_INCOMPLETE_SHADOW_ONLY",
                RequiredMonths, missing, 0, 0, 0, 0, 0, 0, 0, 0,
                false, false, false, [], [],
                "Seis relatórios validados são obrigatórios. Meses ausentes não são zeros nem aprovações.");
        if (monthly.Length != 6 || monthly.Any(m =>
            m.Mode != "EXIT_CAPACITY_GATED_SHADOW_ONLY" ||
            m.PaperModified || m.OrderPlacementEnabled || m.PromotionAllowed ||
            m.Agents != RacMarketSymbols.Supported.Length))
            throw new InvalidDataException("Unsafe or incomplete research study.");
        var timeline = monthly.Select(m => new Month(m.Month,
            m.TotalAgentDays, m.NoTradeDays, m.HypotheticalTrades,
            m.UnresolvedExits, m.RejectedAgents,
            Math.Round(m.Rows.Average(x => x.HypotheticalMeanNetPct), 6)))
            .ToArray();
        var rows = RacMarketSymbols.Supported.Select(symbol =>
        {
            var months = monthly.Select(m => m.Rows.Single(r =>
                r.Symbol == symbol)).ToArray();
            var positives = months.Count(m => m.HypotheticalMeanNetPct > 0m);
            var negatives = months.Count(m => m.HypotheticalMeanNetPct < 0m);
            var flat = months.Length - positives - negatives;
            var n = positives + negatives;
            var p = RacShadowOverfitDiagnostics.OneSidedSignTest(positives, n);
            var adjusted = Math.Min(1m, p * RacMarketSymbols.Supported.Length);
            var unresolved = months.Sum(m => m.UnresolvedExits);
            var active = months.Count(m => m.HypotheticalClosedTrades > 0);
            // Six calendar months is not six independent positive observations.
            // Risk and null-trade gates always override any p-value.
            var status = unresolved > 0 ? "EXIT_UNRESOLVED_REJECTED" :
                active == 0 ? "NO_TRADES" :
                negatives > 0 ? "NEGATIVE_MONTHS" :
                n < 6 ? "INSUFFICIENT_NONZERO_MONTHS" :
                adjusted >= 0.05m ? "NOT_SIGNIFICANT_12_TESTS" :
                "REVIEW_REQUIRED_NOT_APPROVED";
            var researchEligible = status == "REVIEW_REQUIRED_NOT_APPROVED" &&
                months.All(m => m.Gate == "RESEARCH_REVIEW_ONLY");
            return new Agent(symbol, positives, negatives, flat,
                active, months.Sum(m => m.HypotheticalClosedTrades),
                unresolved, p, adjusted,
                months.Min(m => m.HypotheticalMeanNetPct), status,
                researchEligible);
        }).ToArray();
        if (rows.Sum(r => r.HypotheticalTrades) !=
                timeline.Sum(m => m.HypotheticalTrades) ||
            rows.Sum(r => r.UnresolvedExits) !=
                timeline.Sum(m => m.UnresolvedExits))
            throw new InvalidDataException("Research reconciliation failed.");
        return new(true, "SIX_MONTH_WALK_FORWARD_RISK_SHADOW_ONLY",
            RequiredMonths, [], rows.Length, timeline.Sum(m => m.AgentDays),
            timeline.Sum(m => m.NoTradeDays),
            timeline.Sum(m => m.HypotheticalTrades),
            timeline.Sum(m => m.UnresolvedExits),
            rows.Count(r => r.UnresolvedExits > 0),
            rows.Count(r => r.AdjustedP < 0.05m &&
                r.PositiveMonths + r.NegativeMonths >= 6),
            rows.Count(r => r.EligibleForResearchReview),
            false, false, false, timeline, rows,
            "Retornos SHADOW, não dinheiro realizado. Janela de treino e teste diária dentro de cada mês: seis meses não são seis testes independentes. " +
            "O teste de sinais com correção Bonferroni é exploratório e não demonstra ausência de overfitting ou lucro futuro. " +
            "Nenhuma estratégia aprovada para ordens, PAPER ou Testnet.");
    }
}
