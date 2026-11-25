using System.Text.Json;
namespace RAC.Application;
public sealed record RacAnaFinancialReportResult(Guid MessageId,string Status,IReadOnlyList<Guid> EvidenceIds,IReadOnlyList<string> Reasons)
{
    public bool TradingAuthorized=>false;
}
public static class RacAnaFinancialReportReview
{
    public static RacAnaFinancialReportResult Review(RacAgentMessage message)
    {
        RacAnaFinancialReportResult Invalid()=>new(message.Id,"INVALID_FINANCIAL_REPORT",[],["HUMAN_REVIEW_REQUIRED"]);
        if(message.Id==Guid.Empty || message.CorrelationId==Guid.Empty || message.Sender!="carlos" ||
            message.Recipient!="ana" || string.IsNullOrWhiteSpace(message.Content) || message.Content.Length>4096)
            return Invalid();
        try
        {
            using var doc=JsonDocument.Parse(message.Content);
            var r=doc.RootElement;
            if(r.ValueKind!=JsonValueKind.Object || r.EnumerateObject().Select(x=>x.Name).Distinct(StringComparer.Ordinal).Count()!=r.EnumerateObject().Count() ||
                r.GetProperty("Schema").GetString()!="CARLOS_FINANCIAL_V1" ||
                r.GetProperty("Mode").GetString()!="RESEARCH_ONLY" ||
                r.GetProperty("TradingAuthorized").ValueKind!=JsonValueKind.False ||
                r.GetProperty("HumanReviewRequired").ValueKind!=JsonValueKind.True) return Invalid();
            var status=r.GetProperty("Status").GetString();
            if(status is not ("RISK_LOCKED" or "CRITICAL_REVIEW" or "REVIEW_REQUIRED" or "HUMAN_REVIEW_REQUIRED")) return Invalid();
            var kind=status is "RISK_LOCKED" or "CRITICAL_REVIEW"?RacMessageKind.RiskNotice:RacMessageKind.Report;
            if(message.Kind!=kind) return Invalid();
            var ids=r.GetProperty("EvidenceIds").EnumerateArray().Select(x=>x.GetGuid()).ToArray();
            var reasons=r.GetProperty("Reasons").EnumerateArray().Select(x=>x.GetString()??"").ToArray();
            if(reasons.Contains("CAPACITY_EXCEEDED",StringComparer.Ordinal))
            {
                var tasks=new[]{r.GetProperty("RafaelTask").GetGuid(),r.GetProperty("SamuelTask").GetGuid(),r.GetProperty("MiguelTask").GetGuid()};
                var digest=r.GetProperty("EvidenceDigest").GetString();
                var count=r.GetProperty("EvidenceCount").GetInt32();
                if(status!="CRITICAL_REVIEW" || ids.Length!=0 || count<0 || count>1500 ||
                    tasks.Contains(Guid.Empty) || tasks.Distinct().Count()!=3 ||
                    digest is null || digest.Length!=64 || !digest.All(Uri.IsHexDigit) ||
                    reasons.Length!=2 || !reasons.Any(x=>x is "ORIGINAL_STATUS:RISK_LOCKED" or "ORIGINAL_STATUS:CRITICAL_REVIEW" or "ORIGINAL_STATUS:REVIEW_REQUIRED" or "ORIGINAL_STATUS:HUMAN_REVIEW_REQUIRED"))
                    return Invalid();
                return new(message.Id,"CRITICAL_REVIEW",[],Array.AsReadOnly(reasons));
            }
            if(ids.Contains(Guid.Empty) || ids.Distinct().Count()!=ids.Length || reasons.Length==0 ||
                reasons.Any(string.IsNullOrWhiteSpace) || reasons.Distinct(StringComparer.Ordinal).Count()!=reasons.Length ||
                (ids.Length==0 && status!="REVIEW_REQUIRED")) return Invalid();
            return new(message.Id,status,Array.AsReadOnly(ids),Array.AsReadOnly(reasons));
        }
        catch(Exception ex) when(ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException)
        {return Invalid();}
    }
}
