using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class RacShadowOverfitDiagnosticsTests
{
    private static RacShadowThreeMonthDashboard.Result Synthetic(
        decimal[] means, int unresolved = 0, int trades = 3)
    {
        var rows = RacMarketSymbols.Supported.Select((symbol, idx) =>
        {
            var hist = RacShadowThreeMonthDashboard.Months.Select((m, i) =>
                new RacShadowThreeMonthDashboard.AgentMonth(m, 30,
                    trades == 0 ? 0 : 1, idx == 0 && i == 0 ? unresolved : 0,
                    means[i], "REJECTED_OR_INSUFFICIENT_EDGE")).ToArray();
            return new RacShadowThreeMonthDashboard.Agent(symbol,
                idx == 0 ? unresolved : 0, trades,
                means.Average(), false, hist);
        }).ToArray();
        var timeline = RacShadowThreeMonthDashboard.Months.Select(m =>
            new RacShadowThreeMonthDashboard.Month(m, 372, 360, 12,
                trades == 0 ? 0 : 12, m == "2026-08" ? unresolved : 0,
                12, 0, means[Array.IndexOf(RacShadowThreeMonthDashboard.Months, m)]))
            .ToArray();
        return new(true, "THREE_MONTH_EXIT_CAPACITY_SHADOW_ONLY",
            RacShadowThreeMonthDashboard.Months, [], 12, 1104, 1080,
            rows.Sum(x => x.HypotheticalTrades), unresolved, 0,
            false, false, false, timeline, rows, "Synthetic test");
    }

    [Theory]
    [InlineData(3, 3, 0.125)]
    [InlineData(2, 3, 0.5)]
    [InlineData(1, 3, 0.875)]
    [InlineData(0, 3, 1.0)]
    [InlineData(0, 0, 1.0)]
    public void Exact_one_sided_binomial_tail(int positive, int n, double expected)
    {
        Assert.Equal((decimal)expected,
            RacShadowOverfitDiagnostics.OneSidedSignTest(positive, n));
    }

    [Fact]
    public void Three_positive_months_cannot_be_reported_as_statistically_verified()
    {
        var report = RacShadowOverfitDiagnostics.Assess(
            Synthetic([0.5m, 0.7m, 0.2m]));
        Assert.True(report.Available);
        Assert.Equal(12, report.AgentsWithPositiveEveryMonth);
        Assert.Equal(0, report.AgentsStatisticallySupported);
        Assert.Equal(0, report.AgentsEligibleForResearchReview);
        Assert.All(report.Rows, r =>
        {
            Assert.Equal(0.125m, r.OneSidedSignP);
            Assert.Equal(1m, r.BonferroniAdjustedP);
            Assert.Equal("INSUFFICIENT_MONTHS", r.EvidenceStatus);
            Assert.False(r.EligibleForResearchReview);
        });
        Assert.False(report.PaperModified);
        Assert.False(report.OrderPlacementEnabled);
        Assert.False(report.PromotionAllowed);
    }

    [Fact]
    public void Unresolved_exit_risk_takes_precedence_over_nominal_return()
    {
        var report = RacShadowOverfitDiagnostics.Assess(
            Synthetic([1m, 1m, 1m], unresolved: 2));
        Assert.Equal("UNRESOLVED_EXIT_RISK", report.Rows[0].EvidenceStatus);
        Assert.Equal(1, report.AgentsWithUnresolvedExits);
        Assert.Equal(0, report.AgentsEligibleForResearchReview);
    }

    [Fact]
    public void No_trade_is_not_evidence_of_statistical_profit()
    {
        var report = RacShadowOverfitDiagnostics.Assess(
            Synthetic([0m, 0m, 0m], trades: 0));
        Assert.All(report.Rows, x =>
        {
            Assert.Equal("NO_TRADES", x.EvidenceStatus);
            Assert.Equal(1m, x.OneSidedSignP);
            Assert.Equal(0, x.PositiveMonths);
        });
    }

    [Fact]
    public void Missing_report_is_fail_closed_without_fabricating_p_values()
    {
        var empty = RacShadowThreeMonthDashboard.Load(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var result = RacShadowOverfitDiagnostics.Assess(empty);
        Assert.False(result.Available);
        Assert.Empty(result.Rows);
        Assert.False(result.PromotionAllowed);
        Assert.False(result.OrderPlacementEnabled);
    }

    [Fact]
    public void Invalid_sign_test_counts_fail_closed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RacShadowOverfitDiagnostics.OneSidedSignTest(4, 3));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RacShadowOverfitDiagnostics.OneSidedSignTest(-1, 3));
    }
}
