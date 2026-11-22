namespace RAC.Domain;

/// <summary>One idempotent Testnet order per paper fill. Never a real account order.</summary>
public sealed class TestnetOrderRecord
{
    public long Id { get; set; }
    public long PaperFillId { get; set; }
    public required string Symbol { get; set; }
    public required string Side { get; set; }
    public required string ClientOrderId { get; set; }
    public string Status { get; set; } = "PREPARED";
    public string? ExchangeOrderId { get; set; }
    public decimal Quantity { get; set; }
    public decimal ExecutedQuantity { get; set; }
    public decimal CumulativeQuoteQuantity { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public string Note { get; set; } = "";
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>
/// Single-instance pilot state, deliberately disabled after every API startup.
/// Lock is durable and requires a separate reviewed reset in a future version.
/// </summary>
public sealed class TestnetExecutionState
{
    public int Id { get; set; } = 1;
    public bool Enabled { get; set; }
    public bool EmergencyStopped { get; set; }
    public bool CircuitLocked { get; set; }
    public string LockReason { get; set; } = "";
    public DateTimeOffset? EnabledUtc { get; set; }
    public DateTimeOffset? LastCycleUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
}
