using System.Text.Json;
using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class RacShadowThreeMonthDashboardTests
{
    [Fact]
    public void Missing_history_never_claims_zero_returns_or_trading_permission()
    {
        var root = Path.Combine(Path.GetTempPath(), "rac-missing-" + Guid.NewGuid().ToString("N"));
        var result = RacShadowThreeMonthDashboard.Load(root);
        Assert.False(result.Available);
        Assert.Equal(3, result.MissingMonths.Count);
        Assert.Empty(result.Timeline);
        Assert.Empty(result.Rows);
        Assert.False(result.PaperModified);
        Assert.False(result.PromotionAllowed);
        Assert.False(result.OrderPlacementEnabled);
    }

    [Fact]
    public void Three_months_keep_chronology_and_aggregate_without_compounding_profit()
    {
        var root = Path.Combine(Path.GetTempPath(), "rac-3months-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var month in RacShadowThreeMonthDashboard.Months)
            {
                var folder = Path.Combine(root, month);
                Directory.CreateDirectory(folder);
                var days = month == "2026-09" ? 30 : 31;
                var agents = RacMarketSymbols.Supported.Select((symbol, i) => new {
                    symbol, gateResult = i == 0 ? "EXIT_UNRESOLVED_REJECTED" :
                        "REJECTED_OR_INSUFFICIENT_EDGE",
                    totalDays = days, noTradeDays = days - 1, activeDays = 1,
                    selectedClosedTrades = 2, exitIntentCount = 2,
                    exitUnresolvedCount = i == 0 ? 1 : 0,
                    liquidityBlockedEntries = 3, partialEntryFills = 1,
                    netMeanPct = -0.25m
                });
                File.WriteAllText(Path.Combine(folder, "exit-capacity-gated-backtest.json"),
                    JsonSerializer.Serialize(new {
                        mode = "EXIT_CAPACITY_GATED_SHADOW_ONLY", month,
                        paperModified = false, orderPlacementEnabled = false,
                        promotionAllowed = false, count = 12, agents
                    }));
            }
            var report = RacShadowThreeMonthDashboard.Load(root);
            Assert.True(report.Available);
            Assert.Equal("THREE_MONTH_EXIT_CAPACITY_SHADOW_ONLY", report.Mode);
            Assert.Equal(1104, report.TotalAgentDays);
            Assert.Equal(1068, report.NoTradeDays);
            Assert.Equal(72, report.HypotheticalTrades);
            Assert.Equal(3, report.UnresolvedExits);
            Assert.Equal(12, report.AgentCount);
            Assert.Equal(0, report.AgentsEligibleForResearchReview);
            Assert.Equal(new[] { "2026-08", "2026-09", "2026-10" },
                report.Timeline.Select(x => x.MonthKey));
            Assert.All(report.Timeline, x => Assert.Equal(-0.25m, x.MeanHypotheticalReturnPct));
            Assert.All(report.Rows, x => Assert.Equal(-0.25m, x.MeanMonthlyReturnPct));
            Assert.False(report.PromotionAllowed);
            Assert.False(report.OrderPlacementEnabled);
            Assert.False(report.PaperModified);
            File.Delete(Path.Combine(root, "2026-09", "exit-capacity-gated-backtest.json"));
            var incomplete = RacShadowThreeMonthDashboard.Load(root);
            Assert.False(incomplete.Available);
            Assert.Equal(new[] { "2026-09" }, incomplete.MissingMonths);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Report_with_missing_month_produces_no_agent_comparison()
    {
        var root = Path.Combine(Path.GetTempPath(), "rac-short-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "2026-10"));
        try
        {
            var result = RacShadowThreeMonthDashboard.Load(root);
            Assert.False(result.Available);
            Assert.Empty(result.Rows);
        }
        finally { Directory.Delete(root, true); }
    }
}
