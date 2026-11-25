namespace RAC.Domain;

public sealed class DanielDiscoveryRecord
{
    public Guid TaskId { get; set; }
    public DateTimeOffset ObservedUtc { get; set; }
    public string CandidatesJson { get; set; } = "";
    public string? ResearchSnapshotJson { get; set; }
    public int CandidateCount { get; set; }
}
