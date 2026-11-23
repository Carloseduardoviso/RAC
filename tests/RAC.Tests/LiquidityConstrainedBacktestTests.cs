using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class LiquidityConstrainedBacktestTests
{
    private static MarketCandlePoint[] Series(decimal volume, decimal quoteVolume)
    {
        var start = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        return Enumerable.Range(0, 720).Select(i =>
        {
            var close = 100m + i * 0.005m + (i % 31) * 0.07m;
            var t = start.AddMinutes(i);
            return new MarketCandlePoint("BTCUSDT", t,
                t.AddMinutes(1).AddMilliseconds(-1), close,
                close + 1m, close - 1m, close + 0.01m,
                volume, quoteVolume, volume > 0 ? 5 : 0);
        }).ToArray();
    }

    [Fact]
    public void A_zero_volume_candle_does_not_fabricate_executable_trades()
    {
        var bars = Series(0m, 0m);
        var result = PaperTradeHorizonResearch.SimulateSlice("BTCUSDT",
            bars, 15, 1m, "BASELINE", liquidityStress: true);
        Assert.Equal("COMPLETE", result.Status);
        Assert.Equal(0, result.CompletedTrades);
        Assert.Equal(0m, result.ReturnPct);
        Assert.Equal(0, result.PartialEntryFills);
        Assert.True(result.LiquidityBlockedEntries >= 0);
        Assert.Equal(0m, result.MaxDrawdownPct);
    }

    [Fact]
    public void Explicit_no_trade_remains_zero_and_never_enables_orders()
    {
        var result = PaperTradeHorizonResearch.SimulateSlice("BTCUSDT",
            Series(0m, 0m), 30, 2m, "NO_TRADE", liquidityStress: true);
        Assert.Equal("COMPLETE", result.Status);
        Assert.Equal(0, result.CompletedTrades);
        Assert.Equal(0m, result.ReturnPct);
        Assert.Equal(0m, result.TotalUnfilledNotional);
    }

    [Fact]
    public void Synthetic_volume_restriction_is_never_interpreted_as_higher_capacity()
    {
        var low = PaperLiquidityStressAudit.Assess(100m, 101m, 99m,
            1m, 100m, 100m, 0.001m, 1m);
        var high = PaperLiquidityStressAudit.Assess(100m, 101m, 99m,
            10000m, 1000000m, 100m, 0.001m, 1m);
        Assert.True(low.FilledNotional < high.FilledNotional);
        Assert.Equal(100m, high.FilledNotional);
        Assert.False(high.OrderPlacementEnabled);
        Assert.False(high.PromotionAllowed);
    }
}
