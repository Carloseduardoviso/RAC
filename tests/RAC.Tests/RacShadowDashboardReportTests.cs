using System.Text.Json;
using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class RacShadowDashboardReportTests
{
    [Fact]
    public void Missing_monthly_report_is_informative_without_promoting_any_agent()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var report = RacShadowDashboardReport.Load(directory, "2026-10");
        Assert.False(report.Available);
        Assert.Equal(0, report.Agents);
        Assert.False(report.OrderPlacementEnabled);
        Assert.False(report.PromotionAllowed);
        Assert.Empty(report.Rows);
    }

    [Fact]
    public void Valid_twelve_agent_report_has_safe_reconciled_projection()
    {
        var directory = Path.Combine(Path.GetTempPath(), "rac-dashboard-" + Guid.NewGuid().ToString("N"));
        var month = Path.Combine(directory, "2026-10");
        Directory.CreateDirectory(month);
        try
        {
            var agents = RacMarketSymbols.Supported.Select(symbol => new {
                symbol, gateResult = "REJECTED_OR_INSUFFICIENT_EDGE",
                totalDays = 31, noTradeDays = 30, activeDays = 1,
                selectedClosedTrades = 2, exitIntentCount = 2,
                exitUnresolvedCount = 0, liquidityBlockedEntries = 1,
                partialEntryFills = 1, netMeanPct = -0.2m
            }).ToArray();
            File.WriteAllText(Path.Combine(month, "exit-capacity-gated-backtest.json"),
                JsonSerializer.Serialize(new {
                    mode = "EXIT_CAPACITY_GATED_SHADOW_ONLY", month = "2026-10",
                    paperModified = false, orderPlacementEnabled = false,
                    promotionAllowed = false, count = 12, agents
                }));
            var report = RacShadowDashboardReport.Load(directory, "2026-10");
            Assert.True(report.Available);
            Assert.Equal(12, report.Agents);
            Assert.Equal(372, report.TotalAgentDays);
            Assert.Equal(24, report.HypotheticalTrades);
            Assert.Equal(12, report.RejectedAgents);
            Assert.False(report.PromotionAllowed);
            Assert.False(report.PaperModified);
            Assert.False(report.OrderPlacementEnabled);
            Assert.All(report.Rows, row => Assert.Equal(-0.2m, row.HypotheticalMeanNetPct));
            var forbidden = Path.Combine(month, "exit-capacity-gated-backtest.json");
            var text = File.ReadAllText(forbidden).Replace("\"promotionAllowed\":false", "\"promotionAllowed\":true");
            File.WriteAllText(forbidden, text);
            Assert.Throws<InvalidDataException>(() =>
                RacShadowDashboardReport.Load(directory, "2026-10"));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void Future_or_malformed_report_month_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            RacShadowDashboardReport.Load("ignored", "2026-13"));
        Assert.Throws<ArgumentException>(() =>
            RacShadowDashboardReport.Load("ignored", "2030-01"));
    }
}
