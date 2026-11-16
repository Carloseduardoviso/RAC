namespace RAC.Api.Models;

public record RiskPolicy(decimal ReferenceCapitalBrl = 100m, decimal MonthlyContributionBrl = 200m,
    decimal ReinvestmentPct = 100m, decimal MaxCumulativeDrawdownPct = 10m,
    decimal MaxRiskPerTradePct = 0.5m, decimal MaxDailyLossPct = 1m,
    decimal MaxAllocationPerPositionPct = 20m, decimal MaxTotalAllocationPct = 30m,
    int MaxOpenPositions = 1, int MaxEntriesPerDay = 3,
    decimal IllustrativeFeePctPerSide = 0.1m, decimal AssumedBrlPerUsdt = 5m,
    int SimulatedTicksPerDay = 288, int SimulatedDaysPerMonth = 30);
public record TraderView(string Symbol, decimal Price, decimal ChangePct, string Signal, string Status, int Wins, int Losses, decimal RealizedPnl);
public record PositionView(long Id, string Symbol, string Side, decimal Entry, decimal CurrentPrice, decimal Quantity, decimal UnrealizedPnl, decimal StopPrice);
public record ClosedTrade(string Symbol, decimal Pnl, string ExitReason, int ClosedTick);
public record Supervisor(string Name, string Function, string Status, string Detail);
public record RacState(string Project, string Mode, string PriceSource, bool RealOrdersEnabled, bool TestnetOrdersEnabled,
    bool Running, int Tick, decimal StartingBalance, decimal Balance, decimal Equity,
    decimal TotalPnl, decimal DailyPnl, decimal DayDrawdownPct, decimal PeakDrawdownPct,
    decimal TotalContributionsBrl, decimal ProfitAfterContributionsBrl, int ContributedMonths,
    bool RiskLocked, RiskPolicy Settings,
    IReadOnlyList<Supervisor> Supervisors, IReadOnlyList<TraderView> Agents, IReadOnlyList<PositionView> Positions,
    IReadOnlyList<ClosedTrade> Trades, IReadOnlyList<decimal> EquityHistory,
    int EntriesToday, string RiskMessage, DateTimeOffset UpdatedUtc);

