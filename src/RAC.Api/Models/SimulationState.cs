namespace RAC.Api.Models;

// Internal durable state: synthetic simulation only. Do not expose through HTTP.
public sealed record SimTraderState(
    string Symbol, decimal Price, decimal Previous, decimal[] History,
    string Signal, int Wins, int Losses, decimal RealizedPnl);

public sealed record SimPositionState(
    long Id, string Symbol, decimal Entry, decimal Qty, decimal Stop,
    decimal Target, decimal EntryCostBrl, decimal EntryFeeBrl, int OpenedTick);

public sealed record SimEvent(
    Guid Id, string Kind, long? PositionId, string? Symbol,
    decimal Price, decimal Quantity, decimal Fee, decimal AmountBrl,
    string Detail, int Tick);

public sealed record SimulationState(
    int Version, Guid SessionId, RiskPolicy Policy,
    decimal Cash, decimal StartDayEquity, decimal TotalContributions,
    decimal AdjustedPeak, int ContributedMonths, int Tick,
    int EntriesToday, long LastPositionId, bool Running, bool RiskLocked,
    string RiskMessage, SimTraderState[] Traders, SimPositionState[] Positions,
    ClosedTrade[] Trades, decimal[] EquityHistory, SimEvent[] PendingEvents);
