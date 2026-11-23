using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class PaperEntryFilterResearchTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 23, 0, 0, TimeSpan.Zero);
    private static MarketCandlePoint[] Series(int count = 540, bool flat = false)
    {
        var first = Now.AddMinutes(-count);
        return Enumerable.Range(0, count).Select(i =>
        {
            var time = first.AddMinutes(i);
            var price = flat ? 100m : 100m + i * 0.001m + (i % 19) * 0.08m;
            return new MarketCandlePoint("BTCUSDT", time,
                time.AddMinutes(1).AddMilliseconds(-1),
                price, price + 0.3m, price - 0.3m,
                price + (flat ? 0m : 0.02m), 1m, price, 5);
        }).ToArray();
    }

    [Fact]
    public void Filters_choose_from_train_only_and_holdouts_have_no_fake_trades()
    {
        var bars = Series();
        var walk = PaperStrategyWalkForward.Run("BTCUSDT", bars, Now);
        var result = PaperEntryFilterResearch.Run("BTCUSDT", bars, Now, walk);
        Assert.Equal("COMPLETE", result.Status);
        Assert.Equal(walk.FoldCount, result.FoldCount);
        Assert.False(result.PaperModified);
        Assert.False(result.OrderPlacementEnabled);
        Assert.False(result.PromotionAllowed);
        Assert.InRange(result.FilteredSelectionFolds, 0, result.FoldCount);
        Assert.InRange(result.TotalSelectedHoldoutSignals, 0, result.TotalBaselineHoldoutSignals);
        var repeated = PaperEntryFilterResearch.Run("BTCUSDT", bars, Now, walk);
        Assert.Equal(result with { Folds = [] }, repeated with { Folds = [] });
        Assert.Equal(result.Folds.ToArray(), repeated.Folds.ToArray());
        foreach (var fold in result.Folds)
        {
            Assert.Contains(fold.SelectedFromTraining,
                new[] {"BASELINE", "TREND_STRENGTH", "ATR_COST", "TREND_AND_ATR"});
            Assert.True(fold.TrainingFilteredSignals <= fold.TrainingBaselineSignals);
            Assert.True(fold.HoldoutSelectedSignals <= fold.HoldoutBaselineSignals);
            Assert.InRange(fold.HoldoutSelectedPositiveResponses, 0, fold.HoldoutSelectedSignals);
            if (fold.SelectedFromTraining != "BASELINE")
                Assert.True(fold.TrainingFilteredSignals >= PaperEntryFilterResearch.MinimumTrainingSignals);
        }
    }

    [Fact]
    public void No_signals_are_reported_as_missing_not_zero_return()
    {
        var bars = Series(flat: true);
        var walk = PaperStrategyWalkForward.Run("BTCUSDT", bars, Now);
        var report = PaperEntryFilterResearch.Run("BTCUSDT", bars, Now, walk);
        Assert.Equal("COMPLETE", report.Status);
        Assert.Equal(0, report.TotalBaselineHoldoutSignals);
        Assert.Equal(0, report.TotalSelectedHoldoutSignals);
        Assert.Null(report.WeightedSelectedHoldoutMeanNetPct);
        Assert.Null(report.WeightedBaselineHoldoutMeanNetPct);
        Assert.All(report.Folds, f => Assert.Equal("BASELINE", f.SelectedFromTraining));
    }

    [Fact]
    public void Insufficient_or_stale_candles_and_mismatched_walkforward_fail_closed()
    {
        var bars = Series();
        var walk = PaperStrategyWalkForward.Run("BTCUSDT", bars, Now);
        Assert.Equal("INSUFFICIENT_DATA",
            PaperEntryFilterResearch.Run("BTCUSDT", Series(200), Now, walk).Status);
        Assert.Equal("INSUFFICIENT_DATA",
            PaperEntryFilterResearch.Run("BTCUSDT", bars, Now,
                walk with { Symbol = "ETHUSDT" }).Status);
        Assert.Equal("INSUFFICIENT_DATA",
            PaperEntryFilterResearch.Run("BTCUSDT", bars, Now,
                walk with { Status = "INSUFFICIENT_DATA" }).Status);
        var stale = bars.Select(x => x with {
            OpenUtc = x.OpenUtc.AddMinutes(-15), CloseUtc = x.CloseUtc.AddMinutes(-15)
        }).ToArray();
        Assert.Equal("INSUFFICIENT_DATA",
            PaperEntryFilterResearch.Run("BTCUSDT", stale, Now, walk).Status);
        Assert.Throws<ArgumentException>(() =>
            PaperEntryFilterResearch.Run("INVALID", bars, Now, walk));
    }
}
