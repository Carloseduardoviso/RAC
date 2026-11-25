using RAC.Application;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace RAC.Infrastructure;
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
        var kind=result.Status is "RISK_LOCKED" or "CRITICAL_REVIEW"?RacMessageKind.RiskNotice:RacMessageKind.Report;
        var message=RacAgentCommunication.Create("carlos","ana",kind,content,correlationId,observedUtc);
        var key="CARLOS:FINANCIAL:V1:"+correlationId.ToString("N")+":"+content;
        return message with {Id=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(key)).AsSpan(0,16))};
    }
}
