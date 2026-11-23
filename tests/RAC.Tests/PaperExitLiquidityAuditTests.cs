using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class PaperExitLiquidityAuditTests
{
    private static MarketCandlePoint[] Bars(decimal volume, int n = 5)
    {
        var start = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        return Enumerable.Range(0, n).Select(i =>
            new MarketCandlePoint("BTCUSDT", start.AddMinutes(i),
                start.AddMinutes(i + 1).AddMilliseconds(-1), 100m,
                101m, 99m, 100m, volume, volume * 100m,
                volume == 0m ? 0 : 10)).ToArray();
    }

    [Fact]
    public void Zero_volume_never_claims_sale_or_realized_cash()
    {
        var r = PaperExitLiquidityAudit.Replay(1m, Bars(0m), 0.001m, 1m);
        Assert.Equal("UNRESOLVED_EXIT", r.Status);
        Assert.Equal(0m, r.QuantitySold);
        Assert.Equal(1m, r.QuantityUnresolved);
        Assert.Equal(0m, r.NetCashRealized);
        Assert.Equal(90m, r.ResidualMarkToMarketUsdt);
        Assert.Equal(5, r.NoFillAttempts);
        Assert.False(r.AllSold);
        Assert.False(r.PromotionAllowed);
        Assert.False(r.OrderPlacementEnabled);
    }

    [Fact]
    public void Partial_sells_accumulate_without_inventing_cash_for_remainder()
    {
        var r = PaperExitLiquidityAudit.Replay(1m, Bars(100m), 0.001m, 1m);
        Assert.Equal("UNRESOLVED_EXIT", r.Status);
        Assert.Equal(0.5m, r.QuantitySold);
        Assert.Equal(0.5m, r.QuantityUnresolved);
        Assert.Equal(5, r.PartialSellAttempts);
        Assert.True(r.NetCashRealized > 0m);
        Assert.True(r.ResidualMarkToMarketUsdt > 0m);
        Assert.Equal(r.InitialQuantity, r.QuantitySold + r.QuantityUnresolved);
    }

    [Fact]
    public void Sufficient_capacity_completes_sale_and_higher_costs_reduce_proceeds()
    {
        var bars = Bars(10000m);
        var normal = PaperExitLiquidityAudit.Replay(1m, bars, 0.001m, 1m);
        var stressed = PaperExitLiquidityAudit.Replay(1m, bars, 0.001m, 2m);
        Assert.True(normal.AllSold);
        Assert.Equal(0m, normal.QuantityUnresolved);
        Assert.Equal(1, normal.SellAttempts);
        Assert.True(stressed.NetCashRealized < normal.NetCashRealized);
        Assert.Equal(0m, normal.ResidualMarkToMarketUsdt);
    }

    [Fact]
    public void Gaps_fill_at_worse_open_and_invalid_history_fails_closed()
    {
        var bars = Bars(10000m);
        var gap = bars.ToArray();
        gap[0] = gap[0] with { Open = 95m, High = 100m,
            Low = 94m, Close = 95m, QuoteVolume = 950000m };
        var normal = PaperExitLiquidityAudit.Replay(1m, bars, 0.001m, 1m, 99m);
        var low = PaperExitLiquidityAudit.Replay(1m, gap, 0.001m, 1m, 99m);
        Assert.True(low.NetCashRealized < normal.NetCashRealized);
        var broken = bars.ToArray();
        broken[1] = broken[1] with { OpenUtc = broken[1].OpenUtc.AddMinutes(1) };
        Assert.Throws<InvalidDataException>(() =>
            PaperExitLiquidityAudit.Replay(1m, broken, 0.001m, 1m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaperExitLiquidityAudit.Replay(1m, bars, 0.5m, 1m));
    }
}
