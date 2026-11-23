using System.Text.Json;
using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class RacShadowSixMonthValidationTests
{
    [Fact]
    public void Missing_six_month_history_is_ineligible_and_not_zero_profit()
    {
        var r = RacShadowSixMonthValidation.Load(Path.Combine(
            Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        Assert.False(r.Available);
        Assert.Equal(6, r.MissingMonths.Count);
        Assert.Empty(r.Timeline);
        Assert.Empty(r.Rows);
        Assert.False(r.PromotionAllowed);
        Assert.False(r.OrderPlacementEnabled);
    }

    [Fact]
    public void Six_valid_months_preserve_order_and_reject_unresolved_exits()
    {
        var dir = Path.Combine(Path.GetTempPath(),
            "rac-six-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var month in RacShadowSixMonthValidation.RequiredMonths)
            {
                var path = Path.Combine(dir, month);
                Directory.CreateDirectory(path);
                var days = DateTime.DaysInMonth(int.Parse(month[..4]),
                    int.Parse(month[5..]));
                var agents = RacMarketSymbols.Supported.Select((symbol, i) =>
                    new { symbol,
                        gateResult = i == 0 ? "EXIT_UNRESOLVED_REJECTED" :
                            "REJECTED_OR_INSUFFICIENT_EDGE",
                        totalDays = days, noTradeDays = days - 1, activeDays = 1,
                        selectedClosedTrades = 2, exitIntentCount = 2,
                        exitUnresolvedCount = i == 0 ? 1 : 0,
                        liquidityBlockedEntries = 1, partialEntryFills = 1,
                        netMeanPct = 0.2m });
                File.WriteAllText(Path.Combine(path,
                    "exit-capacity-gated-backtest.json"),
                    JsonSerializer.Serialize(new {
                        mode = "EXIT_CAPACITY_GATED_SHADOW_ONLY", month,
                        paperModified = false, orderPlacementEnabled = false,
                        promotionAllowed = false, count = 12, agents
                    }));
            }
            var report = RacShadowSixMonthValidation.Load(dir);
            Assert.True(report.Available);
            Assert.Equal(12, report.Agents);
            Assert.Equal(2208, report.AgentDays);
            Assert.Equal(144, report.HypotheticalTrades);
            Assert.Equal(6, report.UnresolvedExits);
            Assert.Equal(1, report.AgentsWithUnresolvedExits);
            Assert.Equal(0, report.StatisticallySupportedAgents);
            Assert.Equal(0, report.AgentsEligibleForResearchReview);
            Assert.Equal(RacShadowSixMonthValidation.RequiredMonths,
                report.Timeline.Select(x => x.MonthKey));
            Assert.Equal("EXIT_UNRESOLVED_REJECTED", report.Rows[0].EvidenceStatus);
            Assert.Equal("NOT_SIGNIFICANT_12_TESTS", report.Rows[1].EvidenceStatus);
            Assert.Equal(0.015625m, report.Rows[1].OneSidedSignP);
            Assert.Equal(0.1875m, report.Rows[1].AdjustedP);
            Assert.False(report.OrderPlacementEnabled);
            Assert.False(report.PromotionAllowed);
            File.Delete(Path.Combine(dir, "2026-06",
                "exit-capacity-gated-backtest.json"));
            var partial = RacShadowSixMonthValidation.Load(dir);
            Assert.False(partial.Available);
            Assert.Equal(new[] { "2026-06" }, partial.MissingMonths);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [Fact]
    public void Unsafe_flags_are_rejected_before_aggregation()
    {
        var dir = Path.Combine(Path.GetTempPath(),
            "rac-six-safety-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var month in RacShadowSixMonthValidation.RequiredMonths)
            {
                var path = Path.Combine(dir, month);
                Directory.CreateDirectory(path);
                var agents = RacMarketSymbols.Supported.Select(symbol => new {
                    symbol, gateResult = "REJECTED_OR_INSUFFICIENT_EDGE",
                    totalDays = DateTime.DaysInMonth(int.Parse(month[..4]),
                        int.Parse(month[5..])), noTradeDays =
                        DateTime.DaysInMonth(int.Parse(month[..4]),
                            int.Parse(month[5..])), activeDays = 0,
                    selectedClosedTrades = 0, exitIntentCount = 0,
                    exitUnresolvedCount = 0, liquidityBlockedEntries = 0,
                    partialEntryFills = 0, netMeanPct = 0m });
                File.WriteAllText(Path.Combine(path,
                    "exit-capacity-gated-backtest.json"),
                    JsonSerializer.Serialize(new {
                        mode = "EXIT_CAPACITY_GATED_SHADOW_ONLY", month,
                        paperModified = false, orderPlacementEnabled = false,
                        promotionAllowed = month == "2026-07", count = 12,
                        agents
                    }));
            }
            Assert.Throws<InvalidDataException>(() =>
                RacShadowSixMonthValidation.Load(dir));
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
