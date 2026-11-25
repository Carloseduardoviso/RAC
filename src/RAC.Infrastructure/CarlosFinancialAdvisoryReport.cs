using RAC.Application;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace RAC.Infrastructure;
public sealed class CarlosFinancialCapacityException() : InvalidOperationException("Financial advisory exceeds message capacity; complete evidence requires review.");
public static class CarlosFinancialAdvisoryReport
{
    public static RacAgentMessage Create(Guid correlationId,CarlosFinancialEvidenceResult result,DateTimeOffset observedUtc)
    {
        if(result.Status is not ("RISK_LOCKED" or "CRITICAL_REVIEW" or "REVIEW_REQUIRED" or "HUMAN_REVIEW_REQUIRED") ||
            result.EvidenceIds.Any(x=>x==Guid.Empty) || result.Reasons.Count==0 ||
            result.Reasons.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Invalid financial advisory.");
        var ids=result.EvidenceIds.Distinct().OrderBy(x=>x).ToArray();
        var reasons=result.Reasons.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        var content=JsonSerializer.Serialize(new {Schema="CARLOS_FINANCIAL_V1",Mode="RESEARCH_ONLY",
            TradingAuthorized=false,HumanReviewRequired=true,result.Status,EvidenceIds=ids,Reasons=reasons});
        // Never silently truncate evidence. Reject oversized payloads before persistence.
        if(content.Length>4096)
            throw new CarlosFinancialCapacityException();
        if(ids.Length==0 && result.Status!="REVIEW_REQUIRED")
            throw new ArgumentException("Financial advisory requires evidence.");

        var kind=result.Status is "RISK_LOCKED" or "CRITICAL_REVIEW"?RacMessageKind.RiskNotice:RacMessageKind.Report;
        var message=RacAgentCommunication.Create("carlos","ana",kind,content,correlationId,observedUtc);
        var key="CARLOS:FINANCIAL:V1:"+correlationId.ToString("N")+":"+content;
        return message with {Id=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0,16))};
    }
    public static RacAgentMessage CreateForPublication(Guid correlationId,CarlosFinancialEvidenceResult result,
        Guid rafaelTask,Guid samuelTask,Guid miguelTask,DateTimeOffset observedUtc,TimeSpan? evidenceWindow=null)
    {
        try { return Create(correlationId,result,observedUtc); }
        catch(CarlosFinancialCapacityException)
        {
            var canonical=JsonSerializer.Serialize(new {result.Status,
                EvidenceIds=result.EvidenceIds.Distinct().OrderBy(x=>x),
                Reasons=result.Reasons.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal)});
            var digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
            var content=JsonSerializer.Serialize(new {Schema="CARLOS_FINANCIAL_V1",Mode="RESEARCH_ONLY",
                TradingAuthorized=false,HumanReviewRequired=true,Status="CRITICAL_REVIEW",
                EvidenceIds=Array.Empty<Guid>(),Reasons=new[]{"CAPACITY_EXCEEDED","ORIGINAL_STATUS:"+result.Status},
                EvidenceCount=result.EvidenceIds.Distinct().Count(),EvidenceDigest=digest,
                RafaelTask=rafaelTask,SamuelTask=samuelTask,MiguelTask=miguelTask, EvidenceWindowTicks=evidenceWindow?.Ticks});
            var message=RacAgentCommunication.Create("carlos","ana",RacMessageKind.RiskNotice,content,correlationId,observedUtc);
            return message with {Id=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("CARLOS:CAPACITY:V1:"+correlationId.ToString("N")+":"+content)).AsSpan(0,16))};
        }
    }
}
