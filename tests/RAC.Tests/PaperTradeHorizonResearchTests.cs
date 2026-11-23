using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class PaperTradeHorizonResearchTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 23, 0, 0, TimeSpan.Zero);
    private static MarketCandlePoint[] Series(int count = 540, bool flat = false)
    {
        var first = Now.AddMinutes(-count);
        return Enumerable.Range(0, count).Select(i =>
        {
            var t = first.AddMinutes(i);
            var price = flat ? 100m : 100m + i * 0.003m + (i % 19) * 0.08m;
            return new MarketCandlePoint("BTCUSDT", t,
                t.AddMinutes(1).AddMilliseconds(-1), price,
                price + 0.22m, price - 0.22m,
                price + (flat ? 0m : 0.02m), 1m, price, 5);
        }).ToArray();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(30)]
    public void Closed_trades_are_accounted_for_and_holding_horizon_is_bounded(int horizon)
    {
        var bars = Series();
        var result = PaperTradeHorizonResearch.SimulateSlice("BTCUSDT", bars, horizon);
        Assert.Equal("COMPLETE", result.Status);
        Assert.Equal(horizon, result.HorizonMinutes);
        Assert.Equal(result.CompletedTrades, result.WinningTrades + result.LosingTrades);
        Assert.Equal(result.CompletedTrades,
            result.AtrStopExits + result.HorizonExits + result.FinalLiquidations);
        Assert.True(result.MaxDrawdownPct >= 0m);
        Assert.Equal(result, PaperTradeHorizonResearch.SimulateSlice("BTCUSDT", bars, horizon));
        if (result.MeanHoldMinutes is not null)
            Assert.InRange(result.MeanHoldMinutes.Value, 1m, (decimal)horizon);
    }

    [Fact]
    public void Walk_forward_selects_only_on_training_and_never_promotes()
    {
        var bars = Series();
        var walk = PaperStrategyWalkForward.Run("BTCUSDT", bars, Now);
        var report = PaperTradeHorizonResearch.Run("BTCUSDT", bars, Now, walk);
        Assert.Equal("COMPLETE", report.Status);
        Assert.Equal(walk.FoldCount, report.FoldCount);
        Assert.False(report.PaperModified);
        Assert.False(report.OrderPlacementEnabled);
        Assert.False(report.PromotionAllowed);
        Assert.Equal(report.FoldCount, report.Folds.Count);
        Assert.InRange(report.PositiveSelectedHoldouts, 0, report.FoldCount);
        Assert.InRange(report.SelectedNonBaselineFolds, 0, report.FoldCount);
        Assert.Equal(report.CompletedSelectedTrades,
            report.Folds.Sum(x => x.HoldoutSelectedTrades));
        var second = PaperTradeHorizonResearch.Run("BTCUSDT", bars, Now, walk);
        Assert.Equal(report with { Folds = [] }, second with { Folds = [] });
        Assert.Equal(report.Folds.ToArray(), second.Folds.ToArray());
        foreach (var fold in report.Folds)
            Assert.Contains(fold.SelectedHorizonFromTraining, PaperTradeHorizonResearch.Horizons);
    }

    [Fact]
    public void Flat_series_does_not_invent_trades_or_profit()
    {
        var bars = Series(flat: true);
        var walk = PaperStrategyWalkForward.Run("BTCUSDT", bars, Now);
        var report = PaperTradeHorizonResearch.Run("BTCUSDT", bars, Now, walk);
        Assert.Equal("COMPLETE", report.Status);
        Assert.Equal(0, report.CompletedSelectedTrades);
        Assert.Equal(0m, report.MeanSelectedHoldoutReturnPct);
        Assert.All(report.Folds, x => Assert.Equal(1, x.SelectedHorizonFromTraining));
    }

    [Fact]
    public void Missing_stale_or_mismatched_data_fail_closed()
    {
        var bars = Series();
        var walk = PaperStrategyWalkForward.Run("BTCUSDT", bars, Now);
        Assert.Equal("INSUFFICIENT_DATA", PaperTradeHorizonResearch.Run(
            "BTCUSDT", Series(100), Now, walk).Status);
        Assert.Equal("INSUFFICIENT_DATA", PaperTradeHorizonResearch.Run(
            "BTCUSDT", bars, Now, walk with { Symbol = "ETHUSDT" }).Status);
        var old = bars.Select(x => x with { OpenUtc = x.OpenUtc.AddMinutes(-15),
            CloseUtc = x.CloseUtc.AddMinutes(-15) }).ToArray();
        Assert.Equal("INSUFFICIENT_DATA", PaperTradeHorizonResearch.Run(
            "BTCUSDT", old, Now, walk).Status);
        Assert.Equal("BLOCKED", PaperTradeHorizonResearch.SimulateSlice(
            "BTCUSDT", Series(20), 1).Status);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaperTradeHorizonResearch.SimulateSlice("BTCUSDT", bars, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaperTradeHorizonResearch.SimulateSlice("BTCUSDT", bars, 1, 3m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaperTradeHorizonResearch.SimulateSlice("BTCUSDT", bars, 15, 1m, "UNKNOWN"));
        var inactive = PaperTradeHorizonResearch.SimulateSlice("BTCUSDT", bars, 15, 1m, "NO_TRADE");
        Assert.Equal("COMPLETE", inactive.Status);
        Assert.Equal(0, inactive.CompletedTrades);
        Assert.Equal(0m, inactive.ReturnPct);
        Assert.Equal(0m, inactive.MaxDrawdownPct);
        var baseCost = PaperTradeHorizonResearch.SimulateSlice("BTCUSDT", bars, 5, 1m);
        var doubleCost = PaperTradeHorizonResearch.SimulateSlice("BTCUSDT", bars, 5, 2m);
        Assert.True(doubleCost.ReturnPct <= baseCost.ReturnPct);
        Assert.Throws<ArgumentException>(() =>
            PaperTradeHorizonResearch.Run("INVALID", bars, Now, walk));
    }
}
