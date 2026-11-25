using RAC.Application;
using System.Text.Json;
namespace RAC.Infrastructure;

// Advisory interpretation of persisted evidence, not a live balance or authorization.
public sealed record CarlosRafaelReportResult(Guid MessageId,RafaelCapitalAssessment? Capital,string Status)
{
    public bool TradingAuthorized=>false;
}
public static class CarlosRafaelReportReview
{
    public static CarlosRafaelReportResult Review(CarlosRafaelEvidence evidence)
    {
        CarlosRafaelReportResult Invalid()=>new(evidence.MessageId,null,"INVALID_REPORT");
        try
        {
            if(evidence.MessageId==Guid.Empty || evidence.TaskId==Guid.Empty || evidence.Content.Length>4096) return Invalid();
            using var doc=JsonDocument.Parse(evidence.Content);
            var r=doc.RootElement;
            if(r.ValueKind!=JsonValueKind.Object || r.EnumerateObject().Select(x=>x.Name).Distinct(StringComparer.Ordinal).Count()!=r.EnumerateObject().Count()) return Invalid();
            if(r.GetProperty("Mode").GetString()!="RESEARCH_ONLY" || r.GetProperty("TradingAuthorized").ValueKind!=JsonValueKind.False) return Invalid();
            if(r.TryGetProperty("Status",out var status) && status.GetString()=="NO_PAPER_ACCOUNT")
                return new(evidence.MessageId,null,"REVIEW_REQUIRED");
            if(r.GetProperty("Currency").GetString()!="BRL" || r.GetProperty("Valuation").GetString()!="LAST_STORED_PRICE") return Invalid();
            var snapshot=new RafaelCapitalSnapshot(r.GetProperty("Cash").GetDecimal(),r.GetProperty("MarkedPositions").GetDecimal(),
                r.GetProperty("Contributions").GetDecimal(),r.GetProperty("RiskLocked").GetBoolean());
            var capital=RafaelCapitalAnalysis.Evaluate(snapshot);
            var exposure=r.GetProperty("ExposurePercent");
            decimal? reportedExposure=exposure.ValueKind==JsonValueKind.Null?null:exposure.GetDecimal();
            if(capital.Equity!=r.GetProperty("Equity").GetDecimal() || capital.NetResult!=r.GetProperty("NetResult").GetDecimal() ||
                capital.ExposurePercent!=reportedExposure) return Invalid();
            var consistent=r.GetProperty("DataConsistent").GetBoolean();
            var findings=r.GetProperty("Findings");
            if(findings.ValueKind!=JsonValueKind.Array || findings.EnumerateArray().Any(x=>x.ValueKind!=JsonValueKind.String)) return Invalid();
            return new(evidence.MessageId,capital,snapshot.RiskLocked?"RISK_LOCKED":
                !consistent || findings.GetArrayLength()>0?"REVIEW_REQUIRED":"HUMAN_REVIEW_REQUIRED");
        }
        catch(Exception ex) when(ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException or OverflowException)
        { return Invalid(); }
    }
}
