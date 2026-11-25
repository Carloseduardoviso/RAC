using RAC.Application;
namespace RAC.Infrastructure;
public sealed record MiguelAuditReliability(string Status,bool? AccountingConsistent,
    bool? AccountTimestampsFresh,IReadOnlyList<MiguelAuditFinding> Findings)
{
    public bool TradingAuthorized=>false;
    public bool HumanReviewRequired=>Findings.Count>0;
}
public static class MiguelAuditHealth
{
    public static MiguelAuditReliability Evaluate(MiguelStoredAudit? data,DateTimeOffset now,TimeSpan maximumAge)
    {
        if(maximumAge<=TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumAge));
        if(data is null) return new("NO_PAPER_ACCOUNT",null,null,[new("NO_PAPER_ACCOUNT","CRITICAL")]);
        var findings=data.Audit.Findings.ToList();
        bool fresh=true;
        void Check(DateTimeOffset? time,string name)
        {
            if(time is null) {fresh=false; findings.Add(new(name+"_MISSING","WARNING"));}
            else if(time.Value>now) {fresh=false; findings.Add(new(name+"_FUTURE","CRITICAL"));}
            else if(now-time.Value>maximumAge) {fresh=false; findings.Add(new(name+"_STALE","WARNING"));}
        }
        Check(data.UpdatedUtc,"ACCOUNT_UPDATE");
        Check(data.LastCycleUtc,"LAST_CYCLE");
        if(data.LastCycleUtc is DateTimeOffset cycle && cycle>data.UpdatedUtc)
        {fresh=false; findings.Add(new("TIMESTAMP_ORDER_MISMATCH","CRITICAL"));}
        if(data.RiskLocked) findings.Add(new("RISK_LOCKED","WARNING"));
        if(!data.Running) findings.Add(new("PAPER_STOPPED","WARNING"));
        var status=findings.Any(f=>f.Severity=="CRITICAL")?"CRITICAL_REVIEW":
            findings.Count>0?"REVIEW_REQUIRED":"ACCOUNTING_CONSISTENT";
        return new(status,data.Audit.Consistent,fresh,findings.AsReadOnly());
    }
}
