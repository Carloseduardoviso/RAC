using System.Text.Json;
using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class RacRobustnessDashboardReportTests
{
    private static (string Json, RacFrozenDecisionAudit.Result Frozen) Fixture()
    {
        var picks = RacMarketSymbols.Supported.Select(s =>
            new RacFrozenDecisionAudit.Pick(s, "NO_TRADE", 1,
                92, 90, 2, 92, 0, 0m,
                "FROZEN_NO_TRADE_ZERO_EXECUTIONS", false)).ToArray();
        var frozen = new RacFrozenDecisionAudit.Result(true,
            "PRE_HOLDOUT_FROZEN_DECISION_SHADOW_ONLY",
            RacFrozenDecisionAudit.TrainMonths,
            RacFrozenDecisionAudit.HoldoutMonths, [],
            12, 1104, 1104, 12, 0, 0, false, false, false,
            picks, "Research only");
        var rows = RacFrozenDecisionAudit.HoldoutMonths.SelectMany(m =>
            RacMarketSymbols.Supported.Select(s =>
                new RacFixedHoldoutReplay.Result(s, m, "NO_TRADE", 1,
                    DateTime.DaysInMonth(int.Parse(m[..4]), int.Parse(m[5..])),
                    0, 0, 0m, "NO_TRADE_BASELINE_NOT_ACTIVE_REPLAY",
                    false, false, false, false))).ToArray();
        var json = JsonSerializer.Serialize(new {
            mode = "FIXED_POST_TRAINING_SHADOW_ONLY", agents = 12,
            trainingMonths = RacFrozenDecisionAudit.TrainMonths,
            holdoutMonths = RacFrozenDecisionAudit.HoldoutMonths,
            paperModified = false, orderPlacementEnabled = false,
            promotionAllowed = false, rows
        });
        return (json, frozen);
    }

    [Fact]
    public void Projection_is_read_only_and_hides_inactive_profit()
    {
        var (json, frozen) = Fixture();
        var report = RacRobustnessDashboardReport.Parse(json, frozen);
        Assert.True(report.Available);
        Assert.Equal(12, report.AbstainingAgents);
        Assert.All(report.Rows, row => {
            Assert.Null(row.NormalMeanPct);
            Assert.Null(row.DoubleCostMeanPct);
            Assert.False(row.TradingAllowed);
        });
        Assert.False(report.OrderPlacementEnabled);
        Assert.False(report.PromotionAllowed);
    }

    [Fact]
    public void Missing_report_is_unavailable_and_never_approved()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var report = RacRobustnessDashboardReport.Load(root);
        Assert.False(report.Available);
        Assert.Empty(report.Rows);
        Assert.False(report.OrderPlacementEnabled);
    }

    [Fact]
    public void Unsafe_permissions_and_missing_rows_are_rejected()
    {
        var (json, frozen) = Fixture();
        var unsafeJson = json.Replace("\"promotionAllowed\":false",
            "\"promotionAllowed\":true", StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() =>
            RacRobustnessDashboardReport.Parse(unsafeJson, frozen));
        using var doc = JsonDocument.Parse(json);
        var missing = JsonSerializer.Serialize(new {
            mode = "FIXED_POST_TRAINING_SHADOW_ONLY", agents = 12,
            trainingMonths = RacFrozenDecisionAudit.TrainMonths,
            holdoutMonths = RacFrozenDecisionAudit.HoldoutMonths,
            paperModified = false, orderPlacementEnabled = false,
            promotionAllowed = false, rows = Array.Empty<object>()
        });
        Assert.Throws<InvalidDataException>(() =>
            RacRobustnessDashboardReport.Parse(missing, frozen));
    }
}
