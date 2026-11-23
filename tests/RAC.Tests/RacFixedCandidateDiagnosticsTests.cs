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
    public void Robustness_keeps_no_trade_without_active_metrics()
    {
        var (frozen, holdouts) = Fixture();
        var result = RacCandidateRobustnessScreen.Evaluate(frozen, holdouts);
        Assert.Equal(12, result.AbstainingAgents);
        Assert.Equal(0, result.UnstableAgents);
        Assert.All(result.Rows, row =>
        {
            Assert.Equal("NO_TRADE_NO_ACTIVE_EVIDENCE", row.Status);
            Assert.Null(row.NormalMeanPct);
            Assert.Null(row.DoubleCostMeanPct);
            Assert.Null(row.MonthReturnRangePct);
            Assert.False(row.TradingAllowed);
        });
        Assert.False(result.OrderPlacementEnabled);
    }

    [Fact]
    public void Robustness_detects_inconsistent_monthly_returns_and_costs()
    {
        var (frozen, holdouts) = Fixture();
        var picks = frozen.Rows.ToArray();
        picks[0] = picks[0] with {
            FrozenEntryGate = "BASELINE", FrozenHorizonMinutes = 15,
            OutOfSampleSimulatedTrades = null, OutOfSampleReturnPct = null,
            EvaluationStatus = "FIXED_STRATEGY_REPLAY_REQUIRED"
        };
        frozen = frozen with { Rows = picks, FrozenNoTradeAgents = 11,
            StrategiesNeedingReplay = 1 };
        var symbol = picks[0].Symbol;
        var updated = holdouts.Select(x => x.Symbol != symbol ? x : x with {
            FrozenGate = "BASELINE", FrozenHorizonMinutes = 15,
            ClosedTrades = 3, MeanNetReturnPct = x.Month == "2026-09" ? -1m : 2m,
            DoubleCostMeanNetReturnPct = x.Month == "2026-09" ? -2m : 1m,
            EvaluationStatus = "FIXED_SHADOW_REPLAY_RESEARCH_ONLY",
            RiskScreenPassed = true
        }).ToArray();
        var result = RacCandidateRobustnessScreen.Evaluate(frozen, updated);
        var row = result.Rows.Single(x => x.Symbol == symbol);
        Assert.Equal("ACTIVE_ROBUSTNESS_WARNING", row.Status);
        Assert.Equal(2, row.PositiveMonths);
        Assert.Equal(3m, row.MonthReturnRangePct);
        Assert.False(row.TradingAllowed);
        updated = updated.Select(x => x.Symbol == symbol && x.Month == "2026-10"
            ? x with { DoubleCostMeanNetReturnPct = null } : x).ToArray();
        var incomplete = RacCandidateRobustnessScreen.Evaluate(frozen, updated);
        Assert.Equal("ACTIVE_EVIDENCE_INSUFFICIENT",
            incomplete.Rows.Single(x => x.Symbol == symbol).Status);
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
