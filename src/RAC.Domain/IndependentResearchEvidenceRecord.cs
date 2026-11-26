namespace RAC.Domain;
public sealed class IndependentResearchEvidenceRecord
{
    public string ExecutionId {get;set;}="";
    public string FreezeId {get;set;}="";
    public string SourceSha256 {get;set;}="";
    public string Digest {get;set;}="";
    public DateOnly Month {get;set;}
    public string EvidenceJson {get;set;}="";
    public DateTimeOffset RecordedUtc {get;set;}
}
