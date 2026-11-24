namespace RAC.Domain;
public sealed class ResearchExecutionRecord
{
    public long Id { get; set; }
    public string Topic { get; set; } = "";
    public DateTimeOffset StartedUtc { get; set; }
    public long ElapsedMs { get; set; }
    public int? PublicationsFound { get; set; }
    public int? PublicationsSaved { get; set; }
    public int? CandidateDomains { get; set; }
    public string Status { get; set; } = "";
    public string? ErrorCategory { get; set; }
}
