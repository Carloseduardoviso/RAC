namespace RAC.Domain;
public sealed class IndependentResearchFreezeRecord
{
    public string FreezeId {get;set;}="";
    public string ConfigurationId {get;set;}="";
    public string ManifestId {get;set;}="";
    public string EngineVersion {get;set;}="";
    public string Symbol {get;set;}="";
    public string ParametersJson {get;set;}="";
    public DateTimeOffset FrozenUtc {get;set;}
}
