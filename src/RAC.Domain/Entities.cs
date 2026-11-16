namespace RAC.Domain;

public enum OrderStatus { Proposed, Submitted, Filled, PartiallyFilled, Rejected, Cancelled }
public enum OperationMode { Simulation, Testnet, Real }

public sealed class Agent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Role { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class Strategy
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Version { get; set; }
    public bool Enabled { get; set; }
}
public sealed class TradeOrder
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ClientOrderId { get; set; }
    public required string Symbol { get; set; }
    public required string Side { get; set; }
    public decimal Quantity { get; set; }
    public decimal? Price { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Proposed;
    public OperationMode Mode { get; set; } = OperationMode.Simulation;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class TradeExecution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public TradeOrder Order { get; set; } = null!;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Fee { get; set; }
    public DateTimeOffset ExecutedUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class Position
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Symbol { get; set; }
    public decimal Quantity { get; set; }
    public decimal AverageEntry { get; set; }
    public decimal? StopPrice { get; set; }
    public bool IsOpen { get; set; }
    public OperationMode Mode { get; set; } = OperationMode.Simulation;
}
public sealed class Contribution
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public decimal AmountBrl { get; set; }
    public bool IsVirtual { get; set; } = true;
    public DateTimeOffset OccurredUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class EquitySnapshot
{
    public long Id { get; set; }
    public decimal EquityBrl { get; set; }
    public decimal TotalContributionsBrl { get; set; }
    public decimal DrawdownPercent { get; set; }
    public DateTimeOffset RecordedUtc { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class RiskLock
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Reason { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReleasedUtc { get; set; }
}
public sealed class AuditEvent
{
    public long Id { get; set; }
    public required string EventType { get; set; }
    public required string Description { get; set; }
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
