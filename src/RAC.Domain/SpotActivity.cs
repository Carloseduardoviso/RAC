namespace RAC.Domain;

/// <summary>
/// Observed Binance activity, never created by RAC and never an order authorization.
/// The unique external ID is scoped to the owner's account, event type and symbol.
/// </summary>
public sealed class SpotActivity
{
    public long Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string Kind { get; set; } // TRADE, DEPOSIT, WITHDRAWAL
    public required string ReferenceId { get; set; }
    public required string Symbol { get; set; } // Trade pair; empty for transfers
    public required string Asset { get; set; } // Base asset on trade; transfer asset otherwise
    public required string Side { get; set; } // BUY, SELL; empty for transfers
    public required string Status { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal QuoteQuantity { get; set; }
    public decimal Fee { get; set; }
    public required string FeeAsset { get; set; }
    public DateTimeOffset OccurredUtc { get; set; }
    public DateTimeOffset ImportedUtc { get; set; } = DateTimeOffset.UtcNow;
}
