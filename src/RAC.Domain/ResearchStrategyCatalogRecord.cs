namespace RAC.Domain;
public sealed class ResearchStrategyCatalogRecord
{
    public string ContentSha256 {get;set;}="";
    public string SourceSha256 {get;set;}="";
    public string Version {get;set;}="";
    public string CatalogJson {get;set;}="";
    public DateTimeOffset ArchivedUtc {get;set;}
}
