namespace RAC.Application;
public static class ResearchEngineCatalog
{
    public sealed record Version(string Id,string Mode,bool QualityFilters,bool TradingAuthorized);
    public static Version Current {get;}=new(ResearchExperimentReplay.HistoricalEngineVersion,"RESEARCH_ONLY",false,false);
    public static Version Resolve(string id)=>string.Equals(id,Current.Id,StringComparison.Ordinal)
        ?Current:throw new ArgumentException("Unsupported historical engine version.",nameof(id));
}
