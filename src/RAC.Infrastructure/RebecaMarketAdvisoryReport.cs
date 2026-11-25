using RAC.Application;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace RAC.Infrastructure;
public static class RebecaMarketAdvisoryReport
{
    public static RacAgentMessage Create(Guid correlationId,RebecaMarketAdvisoryResult result,DateTimeOffset observedUtc)
    {
        if(result.Status is not ("CRITICAL_REVIEW" or "INCOMPLETE_EVIDENCE" or "HUMAN_REVIEW_REQUIRED") ||
            result.EvidenceIds.Any(x=>x==Guid.Empty) || result.Reasons.Count==0 ||
            result.Reasons.Any(string.IsNullOrWhiteSpace) ||
            (result.EvidenceIds.Count==0 && result.Status!="INCOMPLETE_EVIDENCE"))
            throw new ArgumentException("Invalid market advisory.");
        var ids=result.EvidenceIds.Distinct().OrderBy(x=>x).ToArray();
        var reasons=result.Reasons.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        var content=JsonSerializer.Serialize(new {Schema="REBECA_MARKET_V1",Mode="RESEARCH_ONLY",
            TradingAuthorized=false,HumanReviewRequired=true,result.Status,EvidenceIds=ids,Reasons=reasons});
        if(content.Length>4096) throw new InvalidOperationException("Market advisory exceeds capacity; evidence must not be truncated.");
        // RiskNotice is reserved for Carlos. Rebeca sends an advisory Report.
        var message=RacAgentCommunication.Create("rebeca","ana",RacMessageKind.Report,content,correlationId,observedUtc);
        return message with {Id=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("REBECA:MARKET:V1:"+correlationId.ToString("N")+":"+content)).AsSpan(0,16))};
    }
}
