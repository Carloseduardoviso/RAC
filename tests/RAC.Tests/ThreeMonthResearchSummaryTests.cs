using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class ThreeMonthResearchSummaryTests
{
    [Fact]
    public void Three_month_report_never_promotes_even_when_every_month_is_profitable()
    {
        var months = new[] { new DateOnly(2026, 8, 1),
            new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1) };
        var bases = new List<MonthlyWalkForwardResearch.Result>();
        var stresses = new List<MonthlyWalkForwardResearch.Result>();
        foreach (var month in months)
        foreach (var symbol in RacMarketSymbols.Supported)
        {
            var count = DateTime.DaysInMonth(month.Year, month.Month);
            var days = Enumerable.Range(1, count).Select(day =>
                new MonthlyWalkForwardResearch.Day(
                    new DateOnly(month.Year, month.Month, day), "SIDEWAYS",
                    15, 3, 0.2m, 2, 2, 0, 0, 0.1m, 0.3m, 0.5m)).ToArray();
            var a = new MonthlyWalkForwardResearch.Result(symbol, month.Year,
                month.Month, "COMPLETE", "RESEARCH_REVIEW_ONLY", 1440 * count,
                count, count, count * 2, count * 2, 0, 0, 0.1m, 0.3m, 0.5m,
                false, false, false, days, [], "test");
            bases.Add(a);
            stresses.Add(a with { MeanSelectedTestReturnPct = 0.2m });
        }
        var result = ThreeMonthResearchSummary.Build(months, bases, stresses);
        Assert.Equal(12, result.Count);
        Assert.Equal(0, result.RejectedAgents);
        Assert.Equal(12, result.ReviewOnlyAgents);
        Assert.False(result.PromotionAllowed);
        Assert.False(result.PaperModified);
        Assert.False(result.OrderPlacementEnabled);
        Assert.Equal(0m, result.NoTradeBenchmarkPct);
        Assert.All(result.Agents, a => Assert.Equal(3, a.Months));
    }

    [Fact]
    public void Missing_months_or_nonconsecutive_months_fail_closed()
    {
        var months = new[] { new DateOnly(2026, 8, 1),
            new DateOnly(2026, 9, 1), new DateOnly(2026, 10, 1) };
        Assert.Throws<InvalidDataException>(() => ThreeMonthResearchSummary.Build(
            months, [], []));
        Assert.Throws<ArgumentException>(() => ThreeMonthResearchSummary.Build(
            [months[0], months[2], months[1]], [], []));
    }
}
