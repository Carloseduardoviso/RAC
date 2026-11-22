namespace RAC.Domain;

/// <summary>One immutable observation of the PAPER ONLY engine health.</summary>
public sealed class PaperMonitorSample
{
    public long Id { get; set; }
    public DateTimeOffset RecordedUtc { get; set; }
    public decimal EquityBrl { get; set; }
    public decimal CashBrl { get; set; }
    public decimal ExposureBrl { get; set; }
    public decimal TotalContributionsBrl { get; set; }
    public decimal DrawdownPercent { get; set; }
    public int ReadyAgents { get; set; }
    public int BlockedAgents { get; set; }
    public int OpenPositions { get; set; }
    public bool Running { get; set; }
    public bool RiskLocked { get; set; }
    public DateTimeOffset? LastPaperCycleUtc { get; set; }
}

/// <summary>An audit record of a paper agent's decision for one closed candle.</summary>
public sealed class PaperDecisionAudit
{
    public long Id { get; set; }
    public required string Symbol { get; set; }
    public DateTimeOffset CandleClosedUtc { get; set; }
    public DateTimeOffset RecordedUtc { get; set; }
    public required string Signal { get; set; }
    public required string Outcome { get; set; }
    public required string Actor { get; set; }
    public string Reason { get; set; } = string.Empty;
    public bool PlacedRealOrder { get; set; }
}

/// <summary>Persisted, deduplicated incident. No email or external notification.</summary>
public sealed class PaperMonitorAlert
{
    public long Id { get; set; }
    public required string Code { get; set; }
    public required string Severity { get; set; }
    public required string Message { get; set; }
    public DateTimeOffset OpenedUtc { get; set; }
    public DateTimeOffset LastObservedUtc { get; set; }
    public DateTimeOffset? ResolvedUtc { get; set; }
    public int Observations { get; set; }
}
