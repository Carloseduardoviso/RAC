using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class PaperProfitabilityDiagnosticsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 23, 0, 0, TimeSpan.Zero);
    private static MarketCandlePoint[] Series(int count = 540, bool flat = false)
    {
        var first = Now.AddMinutes(-count);
        return Enumerable.Range(0, count).Select(i =>
        {
            var time = first.AddMinutes(i);
            var price = flat ? 100m : 100m + i * 0.002m + (i % 17) * 0.07m;
            return new MarketCandlePoint("BTCUSDT", time,
                time.AddMinutes(1).AddMilliseconds(-1),
                price, price + 0.20m, price - 0.20m, price + (flat ? 0m : 0.01m),
                1m, price, 5);
        }).ToArray();
    }

    [Fact]
    public void Diagnostic_matches_disjoint_walk_forward_stops_and_never_changes_trading()
    {
        var candles = Series();
        var walk = PaperStrategyWalkForward.Run("BTCUSDT", candles, Now);
        var result = PaperProfitabilityDiagnostics.Run("BTCUSDT", candles, Now, walk);
        Assert.Equal("COMPLETE", walk.Status);
        Assert.Equal("COMPLETE", result.Status);
        Assert.False(result.PaperModified);
        Assert.False(result.OrderPlacementEnabled);
        Assert.Equal(walk.FoldCount, result.HistoricalHoldouts);
        Assert.Equal(walk.Folds.Sum(x => x.HoldoutBaselineStops),
            result.HistoricalHoldoutStopsBaseline);
        Assert.Equal(walk.Folds.Sum(x => x.HoldoutSelectedStops),
            result.HistoricalHoldoutStopsSelected);
        Assert.Equal(result.BuySignalCandles, result.BuyWithNextCandle);
        Assert.Equal(result.BuyWithNextCandle,
            result.LowAtrBuySignals + result.HighAtrBuySignals);
        Assert.InRange(result.NextCandleMovesAboveRoundTripCost, 0, result.BuyWithNextCandle);
        Assert.InRange(result.PositiveNextCandleMoves, 0, result.BuyWithNextCandle);
        Assert.InRange(result.RoundTripCostPct, 0.31m, 0.33m);
        Assert.Equal(result, PaperProfitabilityDiagnostics.Run("BTCUSDT", candles, Now, walk));
    }

    [Fact]
    public void Flat_market_with_no_buy_signals_does_not_invent_profit()
    {
        var candles = Series(flat: true);
        var walk = PaperStrategyWalkForward.Run("BTCUSDT", candles, Now);
        var result = PaperProfitabilityDiagnostics.Run("BTCUSDT", candles, Now, walk);
        Assert.Equal("COMPLETE", result.Status);
        Assert.Equal(0, result.BuySignalCandles);
        Assert.Null(result.MeanNextCandleGrossMovePct);
        Assert.Null(result.MeanNextCandleNetIfRoundTripPct);
    }

    [Fact]
    public void Invalid_history_or_wrong_symbol_blocks_diagnostic()
    {
        var candles = Series();
        var walk = PaperStrategyWalkForward.Run("BTCUSDT", candles, Now);
        Assert.Equal("INSUFFICIENT_DATA", PaperProfitabilityDiagnostics.Run(
            "BTCUSDT", Series(70), Now, walk).Status);
        Assert.Equal("INSUFFICIENT_DATA", PaperProfitabilityDiagnostics.Run(
            "BTCUSDT", candles, Now, walk with { Status = "INSUFFICIENT_DATA" }).Status);
        Assert.Throws<ArgumentException>(() => PaperProfitabilityDiagnostics.Run(
            "INVALID", candles, Now, walk));
        Assert.False(PaperProfitabilityDiagnostics.Run("BTCUSDT", Series(70), Now, walk).PaperModified);
    }
}
