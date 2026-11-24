namespace RAC.Domain;
public sealed class ResearchLicenseRecord
{
    public long Id {get;set;}
    public string Doi {get;set;}="";
    public string Url {get;set;}="";
    public string ContentVersion {get;set;}="";
    public string EvidenceHash {get;set;}="";
    public DateTimeOffset ObservedUtc {get;set;}
}
