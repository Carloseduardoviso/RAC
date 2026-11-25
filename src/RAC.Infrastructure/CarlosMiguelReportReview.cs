using System.Text.Json;
namespace RAC.Infrastructure;
public sealed record CarlosMiguelReportResult(Guid MessageId,string Status,IReadOnlyList<string> Findings)
{
    public bool TradingAuthorized=>false;
}
public static class CarlosMiguelReportReview
{
    public static CarlosMiguelReportResult Review(CarlosMiguelEvidence evidence)
    {
        CarlosMiguelReportResult Invalid()=>new(evidence.MessageId,"INVALID_REPORT",[]);
        try
        {
            if(evidence.MessageId==Guid.Empty || evidence.TaskId==Guid.Empty || evidence.Content.Length>4096) return Invalid();
            using var doc=JsonDocument.Parse(evidence.Content);
            var r=doc.RootElement;
            if(!Unique(r) || r.GetProperty("Mode").GetString()!="RESEARCH_ONLY" ||
                r.GetProperty("TradingAuthorized").ValueKind!=JsonValueKind.False ||
                r.GetProperty("Currency").GetString()!="BRL" || r.GetProperty("Valuation").GetString()!="LAST_STORED_PRICE") return Invalid();
            var count=r.GetProperty("FindingCount").GetInt32();
            var truncated=r.GetProperty("FindingsTruncated").GetBoolean();
            var rows=r.GetProperty("Findings");
            if(count<0 || rows.GetArrayLength()>20) return Invalid();
            var findings=new List<string>();
            var keys=new HashSet<(string,string)>();
            int total=0;
            bool critical=false;
            foreach(var row in rows.EnumerateArray())
            {
                if(!Unique(row)) return Invalid();
                var code=row.GetProperty("Code").GetString();
                var severity=row.GetProperty("Severity").GetString();
                var n=row.GetProperty("Count").GetInt32();
                if(string.IsNullOrWhiteSpace(code) || severity is not ("WARNING" or "CRITICAL") || n<=0 || !keys.Add((code,severity))) return Invalid();
                total=checked(total+n);
                critical|=severity=="CRITICAL";
                findings.Add(severity+":"+code);
            }
            if((!truncated && total!=count) || (truncated && (rows.GetArrayLength()!=20 || total>=count)) ||
                r.GetProperty("HumanReviewRequired").GetBoolean()!=(count>0)) return Invalid();
            var status=r.GetProperty("Status").GetString();
            if(status=="NO_PAPER_ACCOUNT")
                return count==1 && keys.Contains(("NO_PAPER_ACCOUNT","CRITICAL")) && !truncated &&
                    r.GetProperty("CalculatedEquity").ValueKind==JsonValueKind.Null
                    ?new(evidence.MessageId,"CRITICAL_REVIEW",findings.AsReadOnly()):Invalid();
            var consistent=r.GetProperty("AccountingConsistent").GetBoolean();
            var fresh=r.GetProperty("AccountTimestampsFresh").GetBoolean();
            var locked=r.GetProperty("RiskLocked").GetBoolean();
            var running=r.GetProperty("Running").GetBoolean();
            if(count==0 && (!consistent || !fresh || locked || !running)) return Invalid();
            if(!truncated && status!=(critical?"CRITICAL_REVIEW":count>0?"REVIEW_REQUIRED":"ACCOUNTING_CONSISTENT")) return Invalid();
            if(truncated) findings.Add("FINDINGS_TRUNCATED");
            return new(evidence.MessageId,locked?"RISK_LOCKED":critical||truncated?"CRITICAL_REVIEW":
                count>0?"REVIEW_REQUIRED":"HUMAN_REVIEW_REQUIRED",findings.AsReadOnly());
        }
        catch(Exception ex) when(ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException or ArithmeticException)
        {return Invalid();}
    }
    private static bool Unique(JsonElement r)=>r.ValueKind==JsonValueKind.Object &&
        r.EnumerateObject().Select(x=>x.Name).Distinct(StringComparer.Ordinal).Count()==r.EnumerateObject().Count();
}
