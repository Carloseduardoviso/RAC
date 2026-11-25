using System.Security.Cryptography;
using System.Text;
namespace RAC.Application;
public static class ResearchExecutionManifest
{
    public sealed record Evidence(ResearchExperimentPlan.Row Row,DateOnly Month,string SourceSha256);
    public sealed record Manifest(string Id,string EngineVersion,int Assets,int Executions,string Mode,bool TradingAuthorized);
    public static Manifest Create(IReadOnlyList<string> symbols,string engineVersion,IReadOnlyList<Evidence> evidence)
    {
        ResearchEngineCatalog.Resolve(engineVersion);
        var plan=ResearchExperimentPlan.Build(symbols);
        var expected=plan.SelectMany(row=>Enumerable.Range(5,6).Select(month=>(row,new DateOnly(2026,month,1)))).ToHashSet();
        if(evidence.Count!=expected.Count)throw new ArgumentException("Incomplete historical matrix.");
        var ids=new List<string>();
        var sources=new Dictionary<(string,DateOnly),string>();
        foreach(var item in evidence)
        {
            if(!expected.Remove((item.Row,item.Month)))throw new ArgumentException("Unexpected or duplicate execution.");
            ids.Add(ResearchExperimentExecutionIdentity.Create(item.Row,item.SourceSha256,item.Month,engineVersion));
            var key=(item.Row.Symbol,item.Month);
            var hash=item.SourceSha256.ToUpperInvariant();
            if(sources.TryGetValue(key,out var previous)&&previous!=hash)throw new ArgumentException("Conflicting archive provenance.");
            sources[key]=hash;
        }
        if(expected.Count!=0)throw new ArgumentException("Incomplete historical matrix.");
        var canonical="RAC:MANIFEST:V1|"+engineVersion+"|"+string.Join("|",ids.OrderBy(x=>x,StringComparer.Ordinal));
        return new(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))),engineVersion,symbols.Count,ids.Count,"RESEARCH_ONLY",false);
    }
}
