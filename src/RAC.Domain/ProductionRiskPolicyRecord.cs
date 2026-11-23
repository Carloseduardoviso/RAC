namespace RAC.Domain;

/// <summary>
/// Historical production-policy proposal. Approval is documented but cannot
/// authorize a real order; the hard-coded production gate remains disabled.
/// </summary>
public sealed class ProductionRiskPolicyRecord
{
    public long Id { get; set; }
    public int Version { get; set; }
    public string LimitsJson { get; set; } = "";
    public string LimitsSha256 { get; set; } = "";
    public string Status { get; set; } = "PENDING_REVIEW";
    public string ProposedBy { get; set; } = "";
    public DateTimeOffset ProposedUtc { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedUtc { get; set; }
    public string? ReviewReason { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class ProductionRiskPolicyAudit
{
    public long Id { get; set; }
    public long PolicyId { get; set; }
    public string Action { get; set; } = "";
    public string Actor { get; set; } = "";
    public string Reason { get; set; } = "";
    public string LimitsSha256 { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }
}
