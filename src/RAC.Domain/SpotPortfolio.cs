namespace RAC.Domain;

/// <summary>
/// One user-requested observation of a real Binance Spot wallet. Figures are
/// estimates, not trade profits, reconciled statements, or execution authority.
/// </summary>
public sealed class SpotPortfolioSnapshot
{
    public long Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public DateTimeOffset WalletObservedUtc { get; set; }
    public DateTimeOffset QuotesObservedUtc { get; set; }
    public decimal? UsdtBrlRate { get; set; }
    public decimal ValuedSubsetBrl { get; set; }
    public decimal? TotalEstimatedBrl { get; set; }
    public bool CompleteValuation { get; set; }
    public int PricedAssetCount { get; set; }
    public int UnpricedAssetCount { get; set; }
    public ICollection<SpotPortfolioHolding> Holdings { get; set; } = new List<SpotPortfolioHolding>();
}

public sealed class SpotPortfolioHolding
{
    public long Id { get; set; }
    public long SnapshotId { get; set; }
    public SpotPortfolioSnapshot Snapshot { get; set; } = null!;
    public required string Asset { get; set; }
    public decimal Free { get; set; }
    public decimal Locked { get; set; }
    public decimal? UnitPriceBrl { get; set; }
    public decimal? EstimatedValueBrl { get; set; }
    public required string PriceSource { get; set; }
}

/// <summary>
/// Purely manual cash-flow notes, never inferred from Binance or executed.
/// These are not verified deposits/withdrawals or reconciled trading P&L.
/// </summary>
public sealed class SpotCashFlowNote
{
    public long Id { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string Kind { get; set; } // CONTRIBUTION or WITHDRAWAL
    public decimal AmountBrl { get; set; }
    public DateOnly OccurredOn { get; set; }
    public DateTimeOffset RecordedUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Description { get; set; } = string.Empty;
}

public sealed class SpotInvestmentPlan
{
    public Guid OwnerUserId { get; set; }
    public decimal MonthlyContributionBrl { get; set; } = 200m;
    public decimal ReinvestRealizedProfitPercent { get; set; } = 100m;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}
