namespace RAC.Domain;
public sealed class ResearchQuantitativeExecutionRecord
{
    public string ExecutionId {get;set;}="";
    public string ConfigurationId {get;set;}="";
    public string SourceSha256 {get;set;}="";
    public string Symbol {get;set;}="";
    public DateOnly Month {get;set;}
    public string EngineVersion {get;set;}="";
    public string ParametersJson {get;set;}="";
    public string ResultJson {get;set;}="";
    public DateTimeOffset RecordedUtc {get;set;}
}
