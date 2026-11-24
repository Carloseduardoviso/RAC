namespace RAC.Domain;
public sealed class ResearchPublicationRecord
{
    public string Doi { get; set; } = "";
    public string Title { get; set; } = "";
    public string AuthorsJson { get; set; } = "";
    public int? PublicationYear { get; set; }
    public DateTimeOffset DiscoveredUtc { get; set; }
}
