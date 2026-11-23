using RAC.Application;

namespace RAC.Tests;

public sealed class RacFrozenDecisionAuditTests
{
    [Fact]
    public void Majoritarian_no_trade_is_selected_before_holdouts()
    {
        var choices = Enumerable.Repeat((Gate: "NO_TRADE", Horizon: 1), 90)
            .Concat(new[] { (Gate: "TREND_AND_ATR", Horizon: 30),
                            (Gate: "BASELINE", Horizon: 15) }).ToArray();
        var chosen = RacFrozenDecisionAudit.FreezeByVotes(choices);
        Assert.Equal("NO_TRADE", chosen.Gate);
        Assert.Equal(1, chosen.Horizon);
        Assert.Equal(90, chosen.Votes);
    }

    [Fact]
    public void Tie_conservatively_chooses_no_trade()
    {
        var chosen = RacFrozenDecisionAudit.FreezeByVotes(
            new[] { (Gate: "TREND_STRENGTH", Horizon: 30),
                    (Gate: "NO_TRADE", Horizon: 1) });
        Assert.Equal("NO_TRADE", chosen.Gate);
        Assert.Equal(1, chosen.Votes);
    }

    [Fact]
    public void Invalid_or_empty_pre_holdout_choices_fail_closed()
    {
        Assert.Throws<InvalidDataException>(() =>
            RacFrozenDecisionAudit.FreezeByVotes([]));
        Assert.Throws<InvalidDataException>(() =>
            RacFrozenDecisionAudit.FreezeByVotes([
                (Gate: "NO_TRADE", Horizon: 30)]));
        Assert.Throws<InvalidDataException>(() =>
            RacFrozenDecisionAudit.FreezeByVotes([
                (Gate: "UNKNOWN", Horizon: 15)]));
    }

    [Fact]
    public void Missing_research_months_do_not_become_zero_return()
    {
        var result = RacFrozenDecisionAudit.Load(Path.Combine(
            Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        Assert.False(result.Available);
        Assert.Equal(6, result.MissingMonths.Count);
        Assert.Empty(result.Rows);
        Assert.False(result.PaperModified);
        Assert.False(result.OrderPlacementEnabled);
        Assert.False(result.PromotionAllowed);
    }
}
