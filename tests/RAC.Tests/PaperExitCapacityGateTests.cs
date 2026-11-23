using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class PaperExitCapacityGateTests
{
    private static MarketCandlePoint[] Bars(decimal volume, int count = 120)
    {
        var start = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        return Enumerable.Range(0, count).Select(i =>
            new MarketCandlePoint("BTCUSDT", start.AddMinutes(i),
                start.AddMinutes(i + 1).AddMilliseconds(-1),
                100m, 101m, 99m, 100m, volume, volume * 100m,
                volume > 0m ? 10 : 0)).ToArray();
    }
    private static PaperTradeHorizonResearch.Slice Slice(int n,
        params PaperTradeHorizonResearch.ExitIntent[] intents) =>
        new("COMPLETE", n, 15, -0.5m, 0.5m, intents.Length,
            0, intents.Length, 0, intents.Length, 0, 15m,
            ExitIntents: intents);

    [Fact]
    public void A_hypothetical_full_exit_is_only_review_eligible_not_promotion()
    {
        var r = PaperExitCapacityGate.Assess(Slice(120, new PaperTradeHorizonResearch.ExitIntent(50, 1m)),
            Bars(10000m));
        Assert.Equal("EXIT_COVERED_SHADOW_ONLY", r.Status);
        Assert.Equal(1, r.CoveredExits);
        Assert.True(r.ResearchReviewEligible);
        Assert.False(r.PromotionAllowed);
        Assert.False(r.OrderPlacementEnabled);
        Assert.False(r.PaperModified);
    }

    [Fact]
    public void Zero_liquidity_must_fail_closed_without_realized_exit()
    {
        var r = PaperExitCapacityGate.Assess(Slice(120, new PaperTradeHorizonResearch.ExitIntent(50, 1m)), Bars(0m));
        Assert.Equal("EXIT_UNRESOLVED_REJECTED", r.Status);
        Assert.Equal(1, r.UnresolvedExits);
        Assert.Equal(1m, r.TotalUnresolvedQuantity);
        Assert.False(r.ResearchReviewEligible);
    }

    [Fact]
    public void Missing_future_candles_cannot_be_treated_as_completed_sales()
    {
        var r = PaperExitCapacityGate.Assess(Slice(120, new PaperTradeHorizonResearch.ExitIntent(118, 1m)),
            Bars(10000m));
        Assert.Equal(1, r.MissingFutureCandles);
        Assert.Equal(1, r.UnresolvedExits);
        Assert.Equal(0, r.CoveredExits);
        Assert.False(r.ResearchReviewEligible);
    }

    [Fact]
    public void No_trade_is_neither_proof_of_liquidity_nor_permission_to_trade()
    {
        var r = PaperExitCapacityGate.Assess(Slice(120), Bars(10000m));
        Assert.Equal("NO_TRADES", r.Status);
        Assert.False(r.ResearchReviewEligible);
        Assert.Equal(0, r.IntendedExits);
    }

    [Fact]
    public void Missing_trade_events_or_wrong_candle_counts_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() =>
            PaperExitCapacityGate.Assess(Slice(120, new PaperTradeHorizonResearch.ExitIntent(50, 1m)), Bars(1m, 119)));
        Assert.Throws<InvalidDataException>(() =>
            PaperExitCapacityGate.Assess(Slice(120, new PaperTradeHorizonResearch.ExitIntent(120, -1m)), Bars(1m)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaperExitCapacityGate.Assess(Slice(120), Bars(1m), 3m));
    }
}
