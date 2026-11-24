namespace RAC.Domain;
public sealed class ResearchSourceReviewRecord
{
    public long Id { get; set; }
    public string Host { get; set; } = "";
    public DateTimeOffset ReviewedUtc { get; set; }
    public string EvidenceJson { get; set; } = "";
    public int Score { get; set; }
    public string Status { get; set; } = "";
    public string RubricVersion { get; set; } = "1";
}
