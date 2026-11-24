namespace RAC.Domain;
public sealed class ResearchSourceRecord
{
    public string Host { get; set; } = "";
    public string EvidenceUrl { get; set; } = "";
    public string DiscoveredFrom { get; set; } = "";
    public DateTimeOffset DiscoveredUtc { get; set; }
}
