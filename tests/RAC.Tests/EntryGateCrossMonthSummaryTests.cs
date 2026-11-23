using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class EntryGateCrossMonthSummaryTests
{
    private static readonly string[] Months = ["2026-08", "2026-09", "2026-10"];

    private static EntryGateCrossMonthSummary.Month[] Reports(bool positive = false)
    {
        var results = new List<EntryGateCrossMonthSummary.Month>();
        foreach (var month in Months)
        foreach (var symbol in RacMarketSymbols.Supported)
        {
            var n = int.Parse(month[^2..]) == 9 ? 30 : 31;
            results.Add(new EntryGateCrossMonthSummary.Month(symbol, month, n,
                positive ? 0 : n, positive ? n : 0,
                positive ? 3 * n : 0, positive ? n : 0,
                positive ? 0.5m : 0m, -10m,
                false, false, false));
        }
        return results.ToArray();
    }

    [Fact]
    public void All_inactive_days_are_zero_return_not_fabricated_profit()
    {
        var r = EntryGateCrossMonthSummary.Build(Months, Reports());
        Assert.Equal("THREE_MONTH_ENTRY_GATES_SHADOW_ONLY", r.Mode);
        Assert.Equal(12, r.AgentCount);
        Assert.Equal(1104, r.NoTradeDays);
        Assert.Equal(0, r.CompletedTrades);
        Assert.Equal(0, r.PositiveDays);
        Assert.Equal(12, r.RejectedAgents);
        Assert.Equal(0, r.ReviewOnlyAgents);
        Assert.All(r.Agents, a => Assert.Equal(0m, a.MeanNetPct));
        Assert.False(r.PaperModified);
        Assert.False(r.PromotionAllowed);
        Assert.False(r.OrderPlacementEnabled);
    }

    [Fact]
    public void Research_review_is_never_permission_to_trade()
    {
        var r = EntryGateCrossMonthSummary.Build(Months, Reports(positive: true));
        Assert.Equal(12, r.ReviewOnlyAgents);
        Assert.False(r.PromotionAllowed);
        Assert.False(r.PaperModified);
        Assert.False(r.OrderPlacementEnabled);
    }

    [Fact]
    public void Broken_or_unsafe_input_fails_closed()
    {
        var complete = Reports();
        Assert.Throws<InvalidDataException>(() =>
            EntryGateCrossMonthSummary.Build(Months, complete[..^1]));
        Assert.Throws<ArgumentException>(() =>
            EntryGateCrossMonthSummary.Build([Months[0], Months[2], Months[1]], complete));
        complete[0] = complete[0] with { PromotionAllowed = true };
        Assert.Throws<InvalidDataException>(() =>
            EntryGateCrossMonthSummary.Build(Months, complete));
    }
}
