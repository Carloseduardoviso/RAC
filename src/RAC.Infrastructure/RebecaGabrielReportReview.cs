using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace RAC.Infrastructure;
public sealed record RebecaGabrielReportResult(Guid MessageId,string Status,int PositiveMean,int FurtherResearch,string? SourceSha256)
{
    public bool TradingAuthorized=>false;
}
public static class RebecaGabrielReportReview
{
    public static RebecaGabrielReportResult Review(RebecaMarketEvidence evidence)
    {
        RebecaGabrielReportResult Invalid()=>new(evidence.MessageId,"INVALID_REPORT",0,0,null);
        if(evidence.Specialist!="gabriel" || evidence.TaskId==Guid.Empty || evidence.MessageId==Guid.Empty ||
            string.IsNullOrWhiteSpace(evidence.Content) || evidence.Content.Length>4096) return Invalid();
        var expected=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("GABRIEL:REPORT:V1:"+evidence.TaskId.ToString("N"))).AsSpan(0,16));
        if(expected!=evidence.MessageId) return Invalid();
        try
        {
            using var doc=JsonDocument.Parse(evidence.Content);
            var r=doc.RootElement;
            if(!Unique(r) || r.GetProperty("Mode").GetString()!="RESEARCH_ONLY" ||
                r.GetProperty("TradingAuthorized").ValueKind!=JsonValueKind.False ||
                r.GetProperty("IndependentValidation").ValueKind!=JsonValueKind.False ||
                r.GetProperty("Configurations").GetInt32()!=288 || r.GetProperty("StressRanked").GetInt32()!=144) return Invalid();
            var hash=r.GetProperty("SourceSha256").GetString();
            var positive=r.GetProperty("PositiveMean").GetInt32();
            var further=r.GetProperty("FurtherResearch").GetInt32();
            if(hash is null || hash.Length!=64 || !hash.All(Uri.IsHexDigit) || positive<0 || positive>288 ||
                further<0 || further>144 || further>positive) return Invalid();
            var findings=new Dictionary<string,int>(StringComparer.Ordinal);
            foreach(var f in r.GetProperty("Findings").EnumerateArray())
            {
                if(!Unique(f)) return Invalid();
                var status=f.GetProperty("Status").GetString();
                var count=f.GetProperty("Count").GetInt32();
                if(status is not ("UNRESOLVED_POSITIONS" or "NON_POSITIVE_NET_RETURN" or "INSUFFICIENT_TRADES" or "INDEPENDENT_VALIDATION_REQUIRED") ||
                    count<=0 || count>144 || !findings.TryAdd(status,count)) return Invalid();
            }
            if(findings.Values.Sum()!=144 || findings.GetValueOrDefault("INDEPENDENT_VALIDATION_REQUIRED")!=further ||
                further+findings.GetValueOrDefault("INSUFFICIENT_TRADES")>positive) return Invalid();
            return new(evidence.MessageId,"INDEPENDENT_VALIDATION_REQUIRED",positive,further,hash);
        }
        catch(Exception ex) when(ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {return Invalid();}
    }
    private static bool Unique(JsonElement e)=>e.ValueKind==JsonValueKind.Object &&
        e.EnumerateObject().Select(x=>x.Name).Distinct(StringComparer.Ordinal).Count()==e.EnumerateObject().Count();
}
