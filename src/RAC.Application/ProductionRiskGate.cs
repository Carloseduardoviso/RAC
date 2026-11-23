namespace RAC.Application;

/// <summary>
/// Isolated production-readiness policy. This class never places orders and never
/// reads PAPER balances. Every missing approval or observation fails closed.
/// </summary>
public sealed record ProductionRiskLimits(
    decimal MaximumCapitalUsdt,
    decimal MaximumOrderUsdt,
    decimal MaximumDailyLossPercent,
    decimal MaximumDrawdownPercent,
    int MaximumOpenPositions,
    int MaximumOrdersPerHour,
    int MaximumOrdersPerDay,
    IReadOnlySet<string> AllowedSymbols)
{
    public static ProductionRiskLimits Unapproved => new(
        0m, 0m, 0m, 0m, 0, 0, 0, new HashSet<string>(StringComparer.Ordinal));

    public bool IsValid =>
        MaximumCapitalUsdt > 0 && MaximumOrderUsdt > 0 &&
        MaximumOrderUsdt <= MaximumCapitalUsdt &&
        MaximumDailyLossPercent > 0 && MaximumDailyLossPercent <= 100 &&
        MaximumDrawdownPercent > 0 && MaximumDrawdownPercent <= 100 &&
        MaximumOpenPositions > 0 && MaximumOrdersPerHour > 0 &&
        MaximumOrdersPerDay > 0 && AllowedSymbols is { Count: > 0 } &&
        AllowedSymbols.All(x => x.Length is >= 6 and <= 24 &&
            x.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9'));
}

/// <summary>Snapshot must originate from reconciled exchange data, not PAPER.</summary>
public sealed record ProductionRiskSnapshot(
    decimal AccountEquityUsdt,
    decimal DailyStartingEquityUsdt,
    decimal PeakEquityUsdt,
    decimal OpenExposureUsdt,
    int OpenPositions,
    int OrdersLastHour,
    int OrdersToday,
    DateTimeOffset LastAccountSyncUtc,
    DateTimeOffset LastMarketDataUtc,
    bool NetworkHealthy,
    bool ReconciliationComplete,
    bool EmergencyStopped,
    bool LossLockActive,
    bool HumanRiskReviewApproved);

public static class ProductionRiskGate
{
    // No operator action, config value or approval can bypass this release-level guard.
    public static bool RealTradingImplementedAndAuthorized => false;

    public static string? Validate(ProductionRiskLimits limits,
        ProductionRiskSnapshot? state, string symbol, string side,
        decimal orderNotionalUsdt, DateTimeOffset now)
    {
        if (!RealTradingImplementedAndAuthorized)
            return "REAL_TRADING_DISABLED_IN_SOURCE";
        return EvaluateReadiness(limits, state, symbol, side, orderNotionalUsdt, now);
    }

    /// <summary>
    /// Diagnostic evaluation only; never grants permission to submit an order.
    /// A subsequent version must enforce this gate atomically at execution.
    /// </summary>
    public static string? EvaluateReadiness(ProductionRiskLimits limits,
        ProductionRiskSnapshot? state, string symbol, string side,
        decimal orderNotionalUsdt, DateTimeOffset now)
    {
        if (!limits.IsValid) return "RISK_POLICY_NOT_APPROVED";
        if (state is null) return "ACCOUNT_SNAPSHOT_MISSING";
        if (state.EmergencyStopped) return "EMERGENCY_STOP";
        if (state.LossLockActive || !state.HumanRiskReviewApproved)
            return "HUMAN_RISK_APPROVAL_REQUIRED";
        if (!state.NetworkHealthy) return "NETWORK_UNAVAILABLE";
        if (!state.ReconciliationComplete) return "RECONCILIATION_REQUIRED";
        if (state.LastAccountSyncUtc > now || state.LastMarketDataUtc > now ||
            now - state.LastAccountSyncUtc > TimeSpan.FromSeconds(30) ||
            now - state.LastMarketDataUtc > TimeSpan.FromSeconds(30))
            return "STALE_OR_FUTURE_MARKET_OR_ACCOUNT_DATA";
        if (side is not ("BUY" or "SELL") || !limits.AllowedSymbols.Contains(symbol))
            return "SYMBOL_OR_SIDE_NOT_ALLOWED";
        if (orderNotionalUsdt <= 0 || orderNotionalUsdt > limits.MaximumOrderUsdt)
            return "MAX_ORDER_NOTIONAL";
        if (state.AccountEquityUsdt <= 0 || state.DailyStartingEquityUsdt <= 0 ||
            state.PeakEquityUsdt <= 0 || state.OpenExposureUsdt < 0 ||
            state.OpenPositions < 0 || state.OrdersLastHour < 0 || state.OrdersToday < 0)
            return "ACCOUNT_VALUES_INVALID";
        if (state.AccountEquityUsdt > limits.MaximumCapitalUsdt)
            return "CAPITAL_CAP_EXCEEDED";
        if (state.OpenExposureUsdt + orderNotionalUsdt > limits.MaximumCapitalUsdt)
            return "MAX_TOTAL_EXPOSURE";
        if (state.OpenPositions >= limits.MaximumOpenPositions)
            return "MAX_OPEN_POSITIONS";
        if (state.OrdersLastHour >= limits.MaximumOrdersPerHour ||
            state.OrdersToday >= limits.MaximumOrdersPerDay)
            return "ORDER_RATE_LIMIT";
        if ((state.DailyStartingEquityUsdt - state.AccountEquityUsdt) /
            state.DailyStartingEquityUsdt * 100m >= limits.MaximumDailyLossPercent)
            return "DAILY_LOSS_LOCK";
        if ((state.PeakEquityUsdt - state.AccountEquityUsdt) /
            state.PeakEquityUsdt * 100m >= limits.MaximumDrawdownPercent)
            return "DRAWDOWN_LOCK";
        return null;
    }
}
