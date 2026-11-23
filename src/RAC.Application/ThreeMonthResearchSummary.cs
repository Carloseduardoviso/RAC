namespace RAC.Application;

/// <summary>Aggregates only completed independently validated monthly research.
/// Review flags are informative and never authorize trading.</summary>
public static class ThreeMonthResearchSummary
{
    public sealed record Month(string Period, int Days, int PositiveDays,
        int Trades, decimal MeanNetPct, decimal DoubleCostMeanNetPct);
    public sealed record Agent(string Symbol, string ResearchGate,
        int Months, int TestedDays, int PositiveDays, int ClosedTrades,
        decimal MeanNetPct, decimal DoubleCostMeanNetPct,
        decimal WorstDrawdownPct, IReadOnlyList<Month> MonthsDetail);
    public sealed record Result(string Mode, int Count, int Months,
        int TotalAgentDays, int TotalClosedTrades, int PositiveAgentDays,
        int RejectedAgents, int ReviewOnlyAgents, decimal NoTradeBenchmarkPct,
        bool PaperModified, bool RiskLimitsModified, bool OrderPlacementEnabled,
        bool PromotionAllowed, bool HistoryPersistedInOperationalDatabase,
        IReadOnlyList<Agent> Agents, string Interpretation);

    public static Result Build(IReadOnlyList<DateOnly> periods,
        IReadOnlyList<MonthlyWalkForwardResearch.Result> baseRuns,
        IReadOnlyList<MonthlyWalkForwardResearch.Result> doubleCostRuns)
    {
        if (periods.Count != 3 || periods[1] != periods[0].AddMonths(1) ||
            periods[2] != periods[1].AddMonths(1))
            throw new ArgumentException("Exactly three consecutive months required.");
        var symbols = RAC.Domain.RacMarketSymbols.Supported;
        if (baseRuns.Count != 3 * symbols.Length ||
            doubleCostRuns.Count != baseRuns.Count)
            throw new InvalidDataException("All three months and 12 agents are required.");
        var agents = new List<Agent>();
        foreach (var symbol in symbols)
        {
            var rows = new List<Month>();
            var maxDrawdown = 0m;
            foreach (var p in periods)
            {
                var ordinary = Single(baseRuns, symbol, p);
                var stressed = Single(doubleCostRuns, symbol, p);
                if (ordinary.Status != "COMPLETE" || stressed.Status != "COMPLETE" ||
                    ordinary.TestedDays != DateTime.DaysInMonth(p.Year,p.Month) ||
                    stressed.TestedDays != ordinary.TestedDays ||
                    ordinary.PaperModified || stressed.PaperModified ||
                    ordinary.OrderPlacementEnabled || stressed.OrderPlacementEnabled ||
                    ordinary.PromotionAllowed || stressed.PromotionAllowed ||
                    ordinary.CompletedTestTrades != ordinary.Days.Sum(x => x.TestTrades) ||
                    stressed.CompletedTestTrades != stressed.Days.Sum(x => x.TestTrades))
                    throw new InvalidDataException("Unsafe or incomplete monthly results.");
                maxDrawdown = Math.Max(maxDrawdown,
                    Math.Max(ordinary.WorstSelectedTestDrawdownPct,
                        stressed.WorstSelectedTestDrawdownPct));
                rows.Add(new Month(p.ToString("yyyy-MM"), ordinary.TestedDays,
                    ordinary.PositiveTestDays, ordinary.CompletedTestTrades,
                    ordinary.MeanSelectedTestReturnPct,
                    stressed.MeanSelectedTestReturnPct));
            }
            var days = rows.Sum(x => x.Days);
            var weightedBase = Math.Round(rows.Sum(x => x.MeanNetPct * x.Days) / days, 4);
            var weightedStress = Math.Round(rows.Sum(x => x.DoubleCostMeanNetPct * x.Days) / days, 4);
            var positive = rows.Sum(x => x.PositiveDays);
            var trades = rows.Sum(x => x.Trades);
            var positiveMonths = rows.Count(x => x.MeanNetPct > 0m && x.DoubleCostMeanNetPct > 0m);
            // Strict multi-month research-only gate, impossible to auto-promote.
            var considerForResearch = trades >= 90 && positiveMonths == 3 &&
                positive * 10 >= days * 7 && weightedBase > 0m &&
                weightedStress > 0m && maxDrawdown <= 5m;
            agents.Add(new Agent(symbol,
                considerForResearch ? "RESEARCH_REVIEW_ONLY" : "REJECTED_NO_EDGE",
                3, days, positive, trades, weightedBase, weightedStress,
                maxDrawdown, rows));
        }
        return new Result("THREE_MONTH_COST_STRESS_SHADOW_ONLY", agents.Count,
            3, agents.Sum(x => x.TestedDays), agents.Sum(x => x.ClosedTrades),
            agents.Sum(x => x.PositiveDays),
            agents.Count(x => x.ResearchGate == "REJECTED_NO_EDGE"),
            agents.Count(x => x.ResearchGate == "RESEARCH_REVIEW_ONLY"),
            0m, false, false, false, false, false, agents,
            "Three independent UTC monthly archives. Every day resets 100 hypothetical USDT, chooses 1/5/15/30-minute holding horizon only from first 12h training, then scores next 12h. Baseline cost 0.1% fee, 0.05% slippage, 0.01% half-spread per side; stress doubles each. Selection rerun separately for each cost scenario, so strategy under stress need not be the same as baseline. Daily retrospective regime tags cannot be used to trade. REVIEW means investigate only; no PAPER, Testnet or real permission. No-trade 0% is the default comparator.");
    }

    private static MonthlyWalkForwardResearch.Result Single(
        IReadOnlyList<MonthlyWalkForwardResearch.Result> source,
        string symbol, DateOnly month)
    {
        var found = source.Where(x => x.Symbol == symbol && x.Year == month.Year &&
            x.Month == month.Month).ToArray();
        if (found.Length != 1)
            throw new InvalidDataException("Missing or duplicate research month/symbol.");
        return found[0];
    }
}
