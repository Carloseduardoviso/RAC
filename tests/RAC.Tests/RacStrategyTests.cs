using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class RacStrategyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 17, 0, 0, TimeSpan.Zero);

    private static MarketCandlePoint[] Series(int count = 100, decimal step = 0.02m)
    {
        var first = Now.AddMinutes(-count - 1);
        return Enumerable.Range(0, count).Select(i =>
        {
            var opening = 100m + i * step;
            var start = first.AddMinutes(i);
            return new MarketCandlePoint("BTCUSDT", start, start.AddMinutes(1),
                opening, opening + 0.1m, opening - 0.1m,
                opening + step / 2m, 5m, 500m, 10);
        }).ToArray();
    }

    [Fact]
    public void Twelve_supported_symbols_have_a_known_strategy_version()
    {
        Assert.Equal(12, RacMarketSymbols.Supported.Length);
        Assert.Contains("RSI14", RacStrategyEvaluator.Version);
        Assert.Contains("ATR14", RacStrategyEvaluator.Version);
    }

    [Fact]
    public void Closed_trending_series_computes_real_indicators()
    {
        var result = RacStrategyEvaluator.Evaluate("BTCUSDT", Series(), Now);
        Assert.Equal("READY", result.Status);
        Assert.Equal("BINANCE_SPOT_PUBLIC", result.Source);
        Assert.False(result.OrderPlacementEnabled);
        Assert.NotNull(result.Sma9);
        Assert.NotNull(result.Sma21);
        Assert.True(result.Sma9 > result.Sma21);
        Assert.NotNull(result.Atr14);
        Assert.True(result.Atr14 > 0m);
        Assert.InRange(result.Rsi14!.Value, 0m, 100m);
    }

    [Fact]
    public void Gaps_fail_closed_without_trading_signal()
    {
        var data = Series();
        var broken = data.Take(50).Concat(data.Skip(51)).ToArray();
        var result = RacStrategyEvaluator.Evaluate("BTCUSDT", broken, Now);
        Assert.Equal("BLOCKED", result.Status);
        Assert.Equal("SEM_SINAL", result.Action);
    }

    [Fact]
    public void Stale_market_fails_closed()
    {
        var stale = Series().Select(c => c with
        {
            OpenUtc = c.OpenUtc.AddMinutes(-10),
            CloseUtc = c.CloseUtc.AddMinutes(-10)
        }).ToArray();
        var result = RacStrategyEvaluator.Evaluate("BTCUSDT", stale, Now);
        Assert.Equal("BLOCKED", result.Status);
    }

    [Fact]
    public void Not_enough_history_fails_closed()
    {
        var signal = RacStrategyEvaluator.Evaluate("BTCUSDT", Series(15), Now);
        Assert.Equal("BLOCKED", signal.Status);
        var backtest = RacBacktest.Run("BTCUSDT", Series(15), Now);
        Assert.Equal("BLOCKED", backtest.Status);
    }

    [Fact]
    public void Backtest_is_read_only_deterministic_and_cost_aware()
    {
        var data = Series(100);
        var first = RacBacktest.Run("BTCUSDT", data, Now);
        var second = RacBacktest.Run("BTCUSDT", data, Now);
        Assert.Equal(first, second);
        Assert.Equal("COMPLETE", first.Status);
        Assert.Equal(100m, first.InitialUsdt);
        Assert.Equal(0.1m, first.FeePctPerSide);
        Assert.Equal(0.05m, first.SlippagePctPerSide);
        Assert.False(first.OrderPlacementEnabled);
        Assert.InRange(first.MaxDrawdownPct, 0m, 100m);
    }

    [Fact]
    public void Older_collection_gap_does_not_poison_newest_contiguous_segment()
    {
        var all = Series(100);
        var olderGap = all.Take(40).Concat(all.Skip(41)).ToArray();
        var tail = RacStrategyEvaluator.LatestContiguous(olderGap);
        Assert.Equal(59, tail.Length);
        var signal = RacStrategyEvaluator.Evaluate("BTCUSDT", tail, Now);
        Assert.Equal("READY", signal.Status);
        Assert.False(signal.OrderPlacementEnabled);
    }

    [Fact]
    public void Recent_gap_remains_blocked_until_minimum_history_is_collected()
    {
        var all = Series(100);
        var recentGap = all.Take(89).Concat(all.Skip(90)).ToArray();
        var tail = RacStrategyEvaluator.LatestContiguous(recentGap);
        Assert.Equal(10, tail.Length);
        var signal = RacStrategyEvaluator.Evaluate("BTCUSDT", tail, Now);
        Assert.Equal("BLOCKED", signal.Status);
        Assert.Equal("SEM_SINAL", signal.Action);
    }

    [Fact]
    public void Flat_prices_produce_rsi_50_without_dividing_by_zero()
    {
        var series = Series(50, step: 0m);
        Assert.Equal(50m, RacStrategyEvaluator.Rsi(series, 14));
        Assert.Equal("READY",
            RacStrategyEvaluator.Evaluate("BTCUSDT", series, Now).Status);
    }
}
