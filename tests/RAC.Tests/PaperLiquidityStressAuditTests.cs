using RAC.Application;

namespace RAC.Tests;

public sealed class PaperLiquidityStressAuditTests
{
    [Fact]
    public void Zero_volume_fails_closed_with_zero_fill_and_no_cost_fabrication()
    {
        var result = PaperLiquidityStressAudit.Assess(100m, 101m, 99m,
            0m, 0m, 100m, 0.001m, 1m);
        Assert.Equal(0m, result.FilledNotional);
        Assert.Equal(100m, result.UnfilledNotional);
        Assert.Equal(0m, result.FillFraction);
        Assert.Equal(0m, result.EstimatedRoundTripCostPct);
        Assert.True(result.InsufficientLiquidity);
        Assert.False(result.PromotionAllowed);
        Assert.False(result.PaperModified);
        Assert.False(result.OrderPlacementEnabled);
    }

    [Fact]
    public void Participation_limits_are_reconciled_to_notional_and_quantity()
    {
        var r = PaperLiquidityStressAudit.Assess(100m, 101m, 99m,
            20m, 2000m, 100m, 0.01m, 1m);
        Assert.Equal(20m, r.FilledNotional);
        Assert.Equal(80m, r.UnfilledNotional);
        Assert.Equal(0.2m, r.FillFraction);
        Assert.True(r.InsufficientLiquidity);
        Assert.True(r.EffectiveBuyPrice > 100m);
        Assert.True(r.EffectiveSellPrice < 100m);
        Assert.True(r.EstimatedRoundTripCostPct > 0m);
        Assert.Equal(r.RequestedNotional,
            r.FilledNotional + r.UnfilledNotional);
    }

    [Fact]
    public void Higher_stress_cost_and_higher_range_never_improve_round_trip()
    {
        var baseline = PaperLiquidityStressAudit.Assess(100m, 100m, 100m,
            100m, 10000m, 100m, 0.01m, 1m);
        var doubleFee = PaperLiquidityStressAudit.Assess(100m, 100m, 100m,
            100m, 10000m, 100m, 0.01m, 2m);
        var volatileCase = PaperLiquidityStressAudit.Assess(100m, 105m, 95m,
            100m, 10000m, 100m, 0.01m, 1m);
        Assert.Equal(100m, baseline.FilledNotional);
        Assert.True(doubleFee.EstimatedRoundTripCostPct > baseline.EstimatedRoundTripCostPct);
        Assert.True(volatileCase.EstimatedRoundTripCostPct > baseline.EstimatedRoundTripCostPct);
        Assert.Equal(1m, volatileCase.ExtraRangeImpactPct);
    }

    [Fact]
    public void Invalid_volume_range_scenario_and_participation_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaperLiquidityStressAudit.Assess(100m, 90m, 80m, 1m, 100m, 100m, 0.01m, 1m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaperLiquidityStressAudit.Assess(100m, 101m, 99m, -1m, 100m, 100m, 0.01m, 1m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaperLiquidityStressAudit.Assess(100m, 101m, 99m, 1m, 100m, 100m, 0.1m, 1m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaperLiquidityStressAudit.Assess(100m, 101m, 99m, 1m, 100m, 100m, 0.01m, 3m));
    }
}
