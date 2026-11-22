using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class PaperStrategyWalkForwardTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 23, 0, 0, TimeSpan.Zero);

    private static MarketCandlePoint[] Series(int count)
    {
        var first = Now.AddMinutes(-count);
        return Enumerable.Range(0, count).Select(i =>
        {
            var openTime = first.AddMinutes(i);
            var price = 100m + i * 0.001m + (i % 13) * 0.05m;
            return new MarketCandlePoint("BTCUSDT", openTime,
                openTime.AddMinutes(1).AddMilliseconds(-1), price, price + 0.2m,
                price - 0.2m, price + 0.02m, 1m, price, 5);
        }).ToArray();
    }

    [Fact]
    public void Walk_forward_uses_past_training_only_and_disjoint_future_holdouts()
    {
        var result = PaperStrategyWalkForward.Run("BTCUSDT", Series(540), Now);
        Assert.Equal("COMPLETE", result.Status);
        Assert.Equal(3, result.FoldCount);
        Assert.False(result.PromotionAllowed);
        Assert.Equal(540, result.CandleCount);
        Assert.Equal(result.FoldCount,
            result.SelectedBaselineFolds + result.SelectedCooldownFolds);
        for (var i = 0; i < result.Folds.Count; i++)
        {
            var fold = result.Folds[i];
            Assert.True(fold.TrainEndUtc < fold.TestStartUtc);
            Assert.Equal(PaperStrategyWalkForward.TrainCandles,
                (int)(fold.TrainEndUtc - fold.TrainStartUtc).TotalMinutes + 1);
            Assert.Equal(PaperStrategyWalkForward.TestCandles,
                (int)(fold.TestEndUtc - fold.TestStartUtc).TotalMinutes + 1);
            Assert.Equal(fold.TrainingCooldownPct > fold.TrainingBasePct
                ? "COOLDOWN_5" : "BASELINE", fold.SelectedFromTraining);
            Assert.Equal(fold.SelectedFromTraining == "COOLDOWN_5"
                ? fold.HoldoutCandidatePct : fold.HoldoutBaselinePct,
                fold.HoldoutSelectedPct);
            if (i > 0)
                Assert.True(result.Folds[i - 1].TestEndUtc < fold.TestStartUtc);
        }
    }

    [Fact]
    public void Short_stale_or_discontinuous_history_fails_closed()
    {
        var insufficient = PaperStrategyWalkForward.Run("BTCUSDT", Series(419), Now);
        Assert.Equal("INSUFFICIENT_DATA", insufficient.Status);
        Assert.Empty(insufficient.Folds);
        var stale = Series(540).Select(x => x with {
            OpenUtc = x.OpenUtc.AddMinutes(-10),
            CloseUtc = x.CloseUtc.AddMinutes(-10)
        }).ToArray();
        Assert.Equal("INSUFFICIENT_DATA",
            PaperStrategyWalkForward.Run("BTCUSDT", stale, Now).Status);
        var gap = Series(540).Where((_, i) => i != 300).ToArray();
        Assert.Equal("INSUFFICIENT_DATA",
            PaperStrategyWalkForward.Run("BTCUSDT", gap, Now).Status);
        Assert.Equal("INSUFFICIENT_DATA",
            PaperStrategyWalkForward.Run("BTCUSDT", Series(1001), Now).Status);
    }

    [Fact]
    public void Unsupported_pair_cannot_be_evaluated()
        => Assert.Throws<ArgumentException>(() =>
            PaperStrategyWalkForward.Run("NOT_A_PAIR", Series(540), Now));
}
