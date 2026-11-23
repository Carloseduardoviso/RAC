using RAC.Domain;

namespace RAC.Application;

/// <summary>Read-only aggregation of three independently validated monthly
/// SHADOW reports. Never optimizes entry gates using future months.</summary>
public static class RacShadowThreeMonthDashboard
{
    public static readonly string[] Months = ["2026-08", "2026-09", "2026-10"];
    public sealed record Month(string MonthKey, int AgentDays, int NoTradeDays,
        int ActiveDays, int HypotheticalTrades, int UnresolvedExits,
        int RejectedAgents, int ResearchReviewOnlyAgents,
        decimal MeanHypotheticalReturnPct);
    public sealed record AgentMonth(string MonthKey, int NoTradeDays,
        int HypotheticalTrades, int UnresolvedExits,
        decimal HypotheticalMeanReturnPct, string ResearchGate);
    public sealed record Agent(string Symbol, int UnresolvedExits,
        int HypotheticalTrades, decimal MeanMonthlyReturnPct,
        bool AllMonthsResearchReviewEligible,
        IReadOnlyList<AgentMonth> Months);
    public sealed record Result(bool Available, string Mode,
        IReadOnlyList<string> RequiredMonths, IReadOnlyList<string> MissingMonths,
        int AgentCount, int TotalAgentDays, int NoTradeDays,
        int HypotheticalTrades, int UnresolvedExits,
        int AgentsEligibleForResearchReview, bool PaperModified,
        bool OrderPlacementEnabled, bool PromotionAllowed,
        IReadOnlyList<Month> Timeline, IReadOnlyList<Agent> Rows,
        string Notice);

    public static Result Load(string monthlyRoot)
    {
        var monthly = Months.Select(m => RacShadowDashboardReport.Load(monthlyRoot, m))
            .ToArray();
        var missing = monthly.Where(x => !x.Available).Select(x => x.Month).ToArray();
        if (missing.Length > 0)
            return new(false, "INCOMPLETE_THREE_MONTH_SHADOW_ONLY", Months,
                missing, 0, 0, 0, 0, 0, 0, false, false, false,
                [], [], "Histórico incompleto: faltam relatórios validados; valores ausentes não são zero.");
        if (monthly.Length != 3 || monthly.Any(x =>
                x.Mode != "EXIT_CAPACITY_GATED_SHADOW_ONLY" ||
                x.PaperModified || x.OrderPlacementEnabled || x.PromotionAllowed ||
                x.Agents != RacMarketSymbols.Supported.Length))
            throw new InvalidDataException("Unsafe or incomplete monthly comparison.");
        var timeline = monthly.Select(x => new Month(x.Month,
            x.TotalAgentDays, x.NoTradeDays, x.ActiveDays,
            x.HypotheticalTrades, x.UnresolvedExits, x.RejectedAgents,
            x.ReviewOnlyAgents,
            Math.Round(x.Rows.Average(a => a.HypotheticalMeanNetPct), 6)))
            .ToArray();
        var rows = RacMarketSymbols.Supported.Select(symbol =>
        {
            var history = monthly.Select(x =>
            {
                var agent = x.Rows.Single(a => a.Symbol == symbol);
                return new AgentMonth(x.Month, agent.NoTradeDays,
                    agent.HypotheticalClosedTrades, agent.UnresolvedExits,
                    agent.HypotheticalMeanNetPct, agent.Gate);
            }).ToArray();
            // Eligibility is review-only; never permission to execute trades.
            var eligible = history.All(x => x.ResearchGate == "RESEARCH_REVIEW_ONLY") &&
                history.All(x => x.UnresolvedExits == 0);
            return new Agent(symbol, history.Sum(x => x.UnresolvedExits),
                history.Sum(x => x.HypotheticalTrades),
                Math.Round(history.Average(x => x.HypotheticalMeanReturnPct), 6),
                eligible, history);
        }).ToArray();
        return new(true, "THREE_MONTH_EXIT_CAPACITY_SHADOW_ONLY", Months,
            [], rows.Length, timeline.Sum(x => x.AgentDays),
            timeline.Sum(x => x.NoTradeDays),
            timeline.Sum(x => x.HypotheticalTrades),
            timeline.Sum(x => x.UnresolvedExits),
            rows.Count(x => x.AllMonthsResearchReviewEligible),
            false, false, false, timeline, rows,
            "Histórico retrospectivo de agosto a outubro/2026; meses em ordem cronológica, sem reajuste retrospectivo dos sinais. Média mensal não é retorno acumulado ou lucro realizado. Livros de ordens reais não foram observados. Nenhuma autorização para operar.");
    }
}
