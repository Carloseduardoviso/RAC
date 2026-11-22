using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class PaperStrategyShadowComparisonTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 20, 0, 0, TimeSpan.Zero);

    private static MarketCandlePoint[] Series(int count = 200)
    {
        var first = Now.AddMinutes(-count);
        return Enumerable.Range(0, count).Select(i =>
        {
            var start = first.AddMinutes(i);
            var price = 100m + (i % 14) * 0.07m + i * 0.001m;
            return new MarketCandlePoint("BTCUSDT", start, start.AddMinutes(1),
                price, price + 0.20m, price - 0.20m, price + 0.03m,
                1m, price, 5);
        }).ToArray();
    }

    [Fact]
    public void Disjoint_windows_and_shadow_candidate_are_deterministic_and_not_promoted()
    {
        var all = Series();
        var first = PaperStrategyShadowComparison.Run("BTCUSDT", all, Now);
        var second = PaperStrategyShadowComparison.Run("BTCUSDT", all, Now);
        Assert.Equal(first, second);
        Assert.Equal("COMPLETE", first.Status);
        Assert.Equal(all.Length, first.CandleCount);
        Assert.Equal(all.Length / 2, first.EarlierBaseline.Candles);
        Assert.Equal(all.Length / 2, first.LaterBaseline.Candles);
        Assert.Equal("COMPLETE", first.LaterCooldown.Status);
        Assert.False(first.PromotionAllowed);
        Assert.Equal(first.LaterBaseline.Candles, first.LaterCooldown.Candles);
        Assert.InRange(first.LaterBaseline.MaxDrawdownPct, 0m, 100m);
        Assert.True(first.LaterCooldown.SkippedReentries >= 0);
        Assert.True(first.LaterCooldown.AtrStops >= 0);
    }

    [Fact]
    public void Stale_or_gapped_history_cannot_produce_a_valid_optimization()
    {
        var stale = Series().Select(x => x with {
            OpenUtc = x.OpenUtc.AddMinutes(-20),
            CloseUtc = x.CloseUtc.AddMinutes(-20)
        }).ToArray();
        Assert.Equal("INSUFFICIENT_DATA",
            PaperStrategyShadowComparison.Run("BTCUSDT", stale, Now).Status);
        var tooShort = Series(35);
        var blocked = PaperStrategyShadowComparison.Run("BTCUSDT", tooShort, Now);
        Assert.Equal("INSUFFICIENT_DATA", blocked.Status);
        Assert.False(blocked.PromotionAllowed);
        var broken = Series().Where((_,i) => i != 150).ToArray();
        var gapResult = PaperStrategyShadowComparison.Run("BTCUSDT", broken, Now);
        Assert.Equal("INSUFFICIENT_DATA", gapResult.Status);
    }

    [Fact]
    public void Invalid_pair_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            PaperStrategyShadowComparison.Run("INVALID", Series(), Now));
    }
}
