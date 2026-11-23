using RAC.Domain;

namespace RAC.Application;

/// <summary>
/// Pure research-only consolidation of already completed SHADOW entry-gate
/// reports. Never approves paper/testnet/real trading or handles orders.
/// </summary>
public static class EntryGateCrossMonthSummary
{
    public sealed record Month(string Symbol, string MonthKey, int TotalDays,
        int NoTradeDays, int ActiveDays, int CompletedTrades, int PositiveDays,
        decimal MeanNetPct, decimal BaselineMeanNetPct,
        bool PaperModified, bool OrderPlacementEnabled, bool PromotionAllowed);
    public sealed record Agent(string Symbol, string ResearchGate, int Days,
        int NoTradeDays, int ActiveDays, int ClosedTrades, int PositiveDays,
        decimal MeanNetPct, decimal MeanOldBaselinePct,
        IReadOnlyList<Month> Months);
    public sealed record Result(string Mode, int AgentCount, int Months,
        int TotalAgentDays, int NoTradeDays, int ActiveDays, int CompletedTrades,
        int PositiveDays, int RejectedAgents, int ReviewOnlyAgents,
        decimal NoTradeBenchmarkPct, bool PaperModified,
        bool RiskLimitsModified, bool OrderPlacementEnabled,
        bool PromotionAllowed, IReadOnlyList<Agent> Agents,
        string Interpretation);

    public static Result Build(IReadOnlyList<string> keys,
        IReadOnlyList<Month> reports)
    {
        if (keys.Count != 3 || !DateOnly.TryParseExact(keys[0] + "-01",
                "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var first) ||
            !DateOnly.TryParseExact(keys[1] + "-01", "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var second) ||
            !DateOnly.TryParseExact(keys[2] + "-01", "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var third) ||
            second != first.AddMonths(1) || third != second.AddMonths(1))
            throw new ArgumentException("Require three consecutive months.", nameof(keys));
        var symbols = RacMarketSymbols.Supported;
        if (reports.Count != symbols.Length * keys.Count)
            throw new InvalidDataException("Exactly 12 agent reports per month required.");
        var agents = new List<Agent>(symbols.Length);
        foreach (var symbol in symbols)
        {
            var reportsForAgent = new List<Month>();
            for (var i = 0; i < keys.Count; i++)
            {
                var matching = reports.Where(x => x.Symbol == symbol &&
                    x.MonthKey == keys[i]).ToArray();
                if (matching.Length != 1) throw new InvalidDataException(
                    "Missing or duplicate symbol/month research result.");
                var item = matching[0];
                var month = first.AddMonths(i);
                var days = DateTime.DaysInMonth(month.Year, month.Month);
                if (item.TotalDays != days || item.ActiveDays < 0 ||
                    item.NoTradeDays < 0 || item.ActiveDays + item.NoTradeDays != days ||
                    item.CompletedTrades < 0 || item.PositiveDays < 0 ||
                    item.PositiveDays > item.ActiveDays ||
                    (item.ActiveDays == 0 &&
                        (item.CompletedTrades != 0 || item.PositiveDays != 0 ||
                         item.MeanNetPct != 0m)) ||
                    item.PaperModified || item.OrderPlacementEnabled ||
                    item.PromotionAllowed)
                    throw new InvalidDataException("Inconsistent or unsafe monthly research.");
                reportsForAgent.Add(item);
            }
            var total = reportsForAgent.Sum(x => x.TotalDays);
            var active = reportsForAgent.Sum(x => x.ActiveDays);
            var trades = reportsForAgent.Sum(x => x.CompletedTrades);
            var positive = reportsForAgent.Sum(x => x.PositiveDays);
            var mean = Math.Round(reportsForAgent.Sum(x => x.MeanNetPct * x.TotalDays)
                / total, 4);
            var baseline = Math.Round(reportsForAgent.Sum(x =>
                x.BaselineMeanNetPct * x.TotalDays) / total, 4);
            var review = active >= 30 && trades >= 90 &&
                positive * 10 >= total * 7 &&
                reportsForAgent.All(x => x.MeanNetPct > 0m) &&
                mean > 0m && mean > baseline;
            agents.Add(new Agent(symbol,
                review ? "RESEARCH_REVIEW_ONLY" : "REJECTED_OR_INSUFFICIENT_EDGE",
                total, reportsForAgent.Sum(x => x.NoTradeDays), active,
                trades, positive, mean, baseline, reportsForAgent));
        }
        return new Result("THREE_MONTH_ENTRY_GATES_SHADOW_ONLY", agents.Count,
            3, agents.Sum(x => x.Days), agents.Sum(x => x.NoTradeDays),
            agents.Sum(x => x.ActiveDays), agents.Sum(x => x.ClosedTrades),
            agents.Sum(x => x.PositiveDays),
            agents.Count(x => x.ResearchGate == "REJECTED_OR_INSUFFICIENT_EDGE"),
            agents.Count(x => x.ResearchGate == "RESEARCH_REVIEW_ONLY"),
            0m, false, false, false, false, agents,
            "Research report only. Positive results must repeat across all three months, with >=30 active days and >=90 closed simulated trades, >=70% of ALL agent-days positive, and positive means. NO_TRADE=0% is the reference. Results use isolated daily 100 USDT capital, not account equity; no auto promotion to PAPER, Testnet or real orders.");
    }
}
