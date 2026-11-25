using RAC.Application;
using System.Security.Cryptography;
using System.Text;
namespace RAC.Application;

// Explicit advisory requests only; no execution or trading permission.
public static class RacAnaReviewRequests
{
    public static IReadOnlyList<RacAgentMessage> Create(RacAnaAdvisoryResult result, DateTimeOffset now)
    {
        if(result.Status is not ("RISK_REVIEW_REQUIRED" or "INCOMPLETE_REVIEW" or "HUMAN_REVIEW_REQUIRED")) throw new ArgumentException("Invalid advisory status.");
        if(result.EvidenceIds.Count==0) return [];
        var fingerprint=RacAnaAdvisoryFingerprint.Compute(result);
        Guid Key(string purpose)=>new(SHA256.HashData(Encoding.UTF8.GetBytes("ANA:REVIEW:V1:"+fingerprint+":"+purpose)).AsSpan(0,16));
        return Array.AsReadOnly(new[]{"carlos","lucas"}.Select(agent=>RacAgentCommunication.Create("ana",agent,RacMessageKind.TaskRequest,
            System.Text.Json.JsonSerializer.Serialize(new {Schema="ANA_REVIEW_V1",Fingerprint=fingerprint,Status=result.Status,TradingAuthorized=false,HumanReviewRequired=true}),Key("correlation"),now) with {Id=Key(agent)}).ToArray());
    }
}
