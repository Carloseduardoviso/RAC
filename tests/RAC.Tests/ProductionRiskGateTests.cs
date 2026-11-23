using RAC.Application;

namespace RAC.Tests;

public sealed class ProductionRiskGateTests
{
    private static readonly ProductionRiskLimits Limits = new(
        100m, 20m, 1m, 10m, 1, 3, 10,
        new HashSet<string>(StringComparer.Ordinal) { "BTCUSDT", "ETHUSDT" });
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 15, 0, 0, TimeSpan.Zero);

    private static ProductionRiskSnapshot Safe() => new(
        90m, 90m, 90m, 0m, 0, 0, 0, Now, Now,
        true, true, false, false, true);

    private static string? Check(ProductionRiskLimits? limits = null,
        ProductionRiskSnapshot? snapshot = null, string symbol = "BTCUSDT",
        string side = "BUY", decimal notional = 10m) =>
        ProductionRiskGate.EvaluateReadiness(limits ?? Limits,
            snapshot ?? Safe(), symbol, side, notional, Now);

    [Fact]
    public void CannotEnableRealTradingEvenWithHealthyConditions()
    {
        Assert.False(ProductionRiskGate.RealTradingImplementedAndAuthorized);
        Assert.Equal("REAL_TRADING_DISABLED_IN_SOURCE",
            ProductionRiskGate.Validate(Limits, Safe(), "BTCUSDT", "BUY", 10m, Now));
    }

    [Fact]
    public void MissingOrUnapprovedPolicyFailsClosed()
    {
        Assert.Equal("RISK_POLICY_NOT_APPROVED",
            Check(limits: ProductionRiskLimits.Unapproved));
        Assert.Equal("ACCOUNT_SNAPSHOT_MISSING",
            ProductionRiskGate.EvaluateReadiness(Limits, null, "BTCUSDT", "BUY", 10m, Now));
        Assert.Null(Check());
    }

    [Theory]
    [InlineData("XRPUSDT", "BUY", "SYMBOL_OR_SIDE_NOT_ALLOWED")]
    [InlineData("BTCUSDT", "MARGIN", "SYMBOL_OR_SIDE_NOT_ALLOWED")]
    [InlineData("BTCUSDT", "BUY", null)]
    public void RestrictsSymbolsAndOrderSide(string symbol, string side, string? expected) =>
        Assert.Equal(expected, Check(symbol: symbol, side: side));

    [Theory]
    [InlineData(0, "MAX_ORDER_NOTIONAL")]
    [InlineData(21, "MAX_ORDER_NOTIONAL")]
    [InlineData(20, null)]
    public void ChecksOrderLimit(decimal notional, string? expected) =>
        Assert.Equal(expected, Check(notional: notional));

    [Fact]
    public void BlocksStaleAndDisconnectedInputs()
    {
        Assert.Equal("NETWORK_UNAVAILABLE", Check(snapshot: Safe() with { NetworkHealthy = false }));
        Assert.Equal("RECONCILIATION_REQUIRED",
            Check(snapshot: Safe() with { ReconciliationComplete = false }));
        Assert.Equal("STALE_OR_FUTURE_MARKET_OR_ACCOUNT_DATA",
            Check(snapshot: Safe() with { LastMarketDataUtc = Now.AddSeconds(-31) }));
        Assert.Equal("STALE_OR_FUTURE_MARKET_OR_ACCOUNT_DATA",
            Check(snapshot: Safe() with { LastAccountSyncUtc = Now.AddSeconds(1) }));
    }

    [Fact]
    public void StopsOnEmergencyAndRequiresManualReview()
    {
        Assert.Equal("EMERGENCY_STOP", Check(snapshot: Safe() with { EmergencyStopped = true }));
        Assert.Equal("HUMAN_RISK_APPROVAL_REQUIRED",
            Check(snapshot: Safe() with { HumanRiskReviewApproved = false }));
        Assert.Equal("HUMAN_RISK_APPROVAL_REQUIRED",
            Check(snapshot: Safe() with { LossLockActive = true }));
    }

    [Fact]
    public void EnforcesCapitalExposurePositionsAndFrequency()
    {
        Assert.Equal("CAPITAL_CAP_EXCEEDED",
            Check(snapshot: Safe() with { AccountEquityUsdt = 101m }));
        Assert.Equal("MAX_TOTAL_EXPOSURE",
            Check(snapshot: Safe() with { OpenExposureUsdt = 95m }));
        Assert.Equal("MAX_OPEN_POSITIONS",
            Check(snapshot: Safe() with { OpenPositions = 1 }));
        Assert.Equal("ORDER_RATE_LIMIT",
            Check(snapshot: Safe() with { OrdersLastHour = 3 }));
        Assert.Equal("ORDER_RATE_LIMIT",
            Check(snapshot: Safe() with { OrdersToday = 10 }));
    }

    [Fact]
    public void LocksAtDailyLossAndDrawdownThresholds()
    {
        Assert.Equal("DAILY_LOSS_LOCK", Check(snapshot: Safe() with {
            AccountEquityUsdt = 89m, DailyStartingEquityUsdt = 90m
        }));
        Assert.Equal("DRAWDOWN_LOCK", Check(snapshot: Safe() with {
            AccountEquityUsdt = 80m, DailyStartingEquityUsdt = 80m, PeakEquityUsdt = 90m
        }));
    }

    [Fact]
    public void RejectsInvalidAccountNumbersAndUnapprovedLimits()
    {
        Assert.Equal("ACCOUNT_VALUES_INVALID",
            Check(snapshot: Safe() with { OpenExposureUsdt = -1m }));
        Assert.Equal("RISK_POLICY_NOT_APPROVED",
            Check(limits: Limits with { MaximumOrdersPerDay = 0 }));
    }
}
