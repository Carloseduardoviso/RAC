namespace RAC.Domain;
public sealed class ResearchKnowledgeRecord
{
    public long Id {get;set;}
    public string Doi {get;set;}="";
    public string Category {get;set;}="";
    public string Term {get;set;}="";
    public string Evidence {get;set;}="";
    public string EvidenceLevel {get;set;}="TITLE_MENTION";
    public string ExtractorVersion {get;set;}="1";
    public DateTimeOffset ExtractedUtc {get;set;}
}
