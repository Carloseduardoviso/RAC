using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace RAC.Application;

public static class GabrielResearchMessage
{
    public static RacAgentMessage Create(Guid taskId,DateTimeOffset observedUtc,string sourceSha256,
        IReadOnlyList<GabrielExperimentMatrix.Assessment> rows)
    {
        if(sourceSha256.Length!=64 || !sourceSha256.All(Uri.IsHexDigit) || rows.Count!=288)
            throw new ArgumentException("Invalid research evidence.");
        var ranked=GabrielStrategyRanking.Rank(rows);
        if(ranked.Count!=144) throw new ArgumentException("Invalid stress matrix.");
        var content=JsonSerializer.Serialize(new {Mode="RESEARCH_ONLY",TradingAuthorized=false,IndependentValidation=false,
            SourceSha256=sourceSha256.ToUpperInvariant(),Configurations=rows.Count,StressRanked=ranked.Count,
            PositiveMean=rows.Count(x=>x.Evidence.MeanNetPercent>0),
            FurtherResearch=ranked.Count(x=>x.Status=="INDEPENDENT_VALIDATION_REQUIRED"),
            Findings=ranked.GroupBy(x=>x.Status).OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>new {Status=x.Key,Count=x.Count()}).ToArray()});
        var message=RacAgentCommunication.Create("gabriel","rebeca",RacMessageKind.Report,content,taskId,observedUtc);
        var hash=SHA256.HashData(Encoding.UTF8.GetBytes("GABRIEL:REPORT:V1:"+taskId.ToString("N")));
        return message with {Id=new Guid(hash.AsSpan(0,16))};
    }
}
