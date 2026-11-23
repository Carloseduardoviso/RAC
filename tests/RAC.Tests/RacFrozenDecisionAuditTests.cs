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
    public void Active_gate_ties_are_deterministic_and_never_use_holdout_returns()
    {
        var first = new[] {
            (Gate: "TREND_STRENGTH", Horizon: 30),
            (Gate: "ATR_COST", Horizon: 15),
            (Gate: "TREND_STRENGTH", Horizon: 30),
            (Gate: "ATR_COST", Horizon: 15)
        };
        var forward = RacFrozenDecisionAudit.FreezeByVotes(first);
        var reverse = RacFrozenDecisionAudit.FreezeByVotes(first.Reverse().ToArray());
        Assert.Equal(forward, reverse);
        Assert.Equal("ATR_COST", forward.Gate);
        Assert.Equal(15, forward.Horizon);
        Assert.Equal(2, forward.Votes);
    }

    [Fact]
    public void Majority_rule_does_not_ignore_invalid_minority_choices()
    {
        var choices = Enumerable.Repeat((Gate: "NO_TRADE", Horizon: 1), 91)
            .Append((Gate: "BASELINE", Horizon: 1)).ToArray();
        Assert.Throws<InvalidDataException>(() =>
            RacFrozenDecisionAudit.FreezeByVotes(choices));
    }

    [Fact]
    public void No_trade_with_wrong_horizon_is_never_selected()
    {
        Assert.Throws<InvalidDataException>(() =>
            RacFrozenDecisionAudit.FreezeByVotes([
                (Gate: "NO_TRADE", Horizon: 15),
                (Gate: "NO_TRADE", Horizon: 1)
            ]));
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
