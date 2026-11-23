using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class RacFixedCandidateDiagnosticsTests
{
    private static (RacFrozenDecisionAudit.Result Frozen,
        RacFixedHoldoutReplay.Result[] Holdouts) Fixture()
    {
        var picks = RacMarketSymbols.Supported.Select(symbol =>
            new RacFrozenDecisionAudit.Pick(symbol, "NO_TRADE", 1,
                92, 89, 3, 92, 0, 0m,
                "FROZEN_NO_TRADE_ZERO_EXECUTIONS", false)).ToArray();
        var frozen = new RacFrozenDecisionAudit.Result(true,
            "PRE_HOLDOUT_FROZEN_DECISION_SHADOW_ONLY",
            RacFrozenDecisionAudit.TrainMonths,
            RacFrozenDecisionAudit.HoldoutMonths, [],
            picks.Length, picks.Length * 92, picks.Length * 92,
            picks.Length, 0, 0, false, false, false, picks, "Research only");
        var holdouts = RacFrozenDecisionAudit.HoldoutMonths
            .SelectMany(month => RacMarketSymbols.Supported.Select(symbol =>
                new RacFixedHoldoutReplay.Result(symbol, month, "NO_TRADE",
                    1, DateTime.DaysInMonth(int.Parse(month[..4]),
                        int.Parse(month[5..])), 0, 0, 0m,
                    "NO_TRADE_BASELINE_NOT_ACTIVE_REPLAY",
                    false, false, false, false))).ToArray();
        return (frozen, holdouts);
    }

    [Fact]
    public void No_trade_is_never_misrepresented_as_active_return()
    {
        var (frozen, holdouts) = Fixture();
        var result = RacFixedCandidateDiagnostics.Compare(frozen, holdouts);
        Assert.Equal(12, result.NoTradeAgents);
        Assert.Equal(0, result.ActiveAgentsWithMeasuredReturn);
        Assert.Equal(0, result.ActiveAgentsMissingEvidence);
        Assert.Equal(12, result.Rows.Count);
        Assert.All(result.Rows, x =>
        {
            Assert.Equal("NO_TRADE_ABSTENTION_BASELINE", x.DiagnosticStatus);
            Assert.Null(x.MeanHoldoutReturnPct);
            Assert.Null(x.MeanDoubleCostReturnPct);
            Assert.False(x.EligibleForTrading);
            Assert.Equal(92, x.HoldoutDays);
        });
        Assert.False(result.PromotionAllowed);
        Assert.False(result.OrderPlacementEnabled);
    }

    [Fact]
    public void Missing_month_and_changed_gate_fail_closed()
    {
        var (frozen, holdouts) = Fixture();
        Assert.Throws<InvalidDataException>(() =>
            RacFixedCandidateDiagnostics.Compare(frozen, holdouts.Skip(1).ToArray()));
        var modified = holdouts.ToArray();
        modified[0] = modified[0] with { FrozenGate = "BASELINE" };
        Assert.Throws<InvalidDataException>(() =>
            RacFixedCandidateDiagnostics.Compare(frozen, modified));
    }

    [Fact]
    public void Fabricated_no_trade_profit_is_rejected()
    {
        var (frozen, holdouts) = Fixture();
        var modified = holdouts.ToArray();
        modified[0] = modified[0] with { MeanNetReturnPct = 2m };
        Assert.Throws<InvalidDataException>(() =>
            RacFixedCandidateDiagnostics.Compare(frozen, modified));
    }
}
