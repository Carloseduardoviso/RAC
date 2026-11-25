namespace RAC.Domain;
public sealed class ResearchExecutionManifestRecord
{
    public string ManifestId {get;set;}="";
    public string EngineVersion {get;set;}="";
    public int Assets {get;set;}
    public int Executions {get;set;}
    public string EvidenceJson {get;set;}="";
    public DateTimeOffset RecordedUtc {get;set;}
}
