namespace RAC.Domain;

/// <summary>Independent BRL-denominated paper account. Never a Binance wallet.</summary>
public sealed class PaperAccount
{
    public int Id { get; set; } = 1;
    public decimal CashBrl { get; set; } = 100m;
    public decimal TotalContributionsBrl { get; set; } = 100m;
    public decimal PeakEquityBrl { get; set; } = 100m;
    public decimal DailyStartEquityBrl { get; set; } = 100m;
    public DateOnly CurrentDayUtc { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateTimeOffset NextContributionUtc { get; set; } = DateTimeOffset.UtcNow.AddDays(30);
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public bool Running { get; set; }
    public bool RiskLocked { get; set; }
    public string RiskReason { get; set; } = string.Empty;
    public decimal LastEquityBrl { get; set; } = 100m;
    public int EntriesToday { get; set; }
    public DateTimeOffset? LastCycleUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class PaperPosition
{
    public long Id { get; set; }
    public required string Symbol { get; set; }
    public decimal Quantity { get; set; }
    public decimal EntryPriceBrl { get; set; }
    public decimal StopPriceBrl { get; set; }
    public decimal EntryFeeBrl { get; set; }
    public decimal LastPriceBrl { get; set; }
    public DateTimeOffset OpenedUtc { get; set; }
    public DateTimeOffset SignalClosedUtc { get; set; }
}

public sealed class PaperFill
{
    public long Id { get; set; }
    public required string Symbol { get; set; }
    public required string Side { get; set; }
    public required string Reason { get; set; }
    public decimal Quantity { get; set; }
    public decimal PriceBrl { get; set; }
    public decimal FeeBrl { get; set; }
    public DateTimeOffset ExecutedUtc { get; set; }
    public DateTimeOffset SignalClosedUtc { get; set; }
    public decimal? RealizedPnlBrl { get; set; }
}

public sealed class PaperAgentCursor
{
    public required string Symbol { get; set; }
    public DateTimeOffset LastCandleUtc { get; set; }
    public required string LastSignal { get; set; }
    public required string Status { get; set; }
    public string Note { get; set; } = string.Empty;
}
