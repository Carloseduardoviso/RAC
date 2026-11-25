using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class MiguelAuditHealthTests
{
    [Fact]
    public void DistinguishesAccountingConsistencyFromFreshnessAndRisk()
    {
        var now=DateTimeOffset.UtcNow;
        var audit=MiguelIntegrityAudit.Evaluate(100,100,100,[],[],0.01m,0);
        var data=new MiguelStoredAudit(audit,false,true,now,now,0,0);
        var age=TimeSpan.FromMinutes(2);
        var valid=MiguelAuditHealth.Evaluate(data,now,age);
        Assert.Equal("ACCOUNTING_CONSISTENT",valid.Status);
        Assert.False(valid.TradingAuthorized);
        Assert.False(valid.HumanReviewRequired);
        var stale=MiguelAuditHealth.Evaluate(data with {UpdatedUtc=now.AddMinutes(-3),LastCycleUtc=null,RiskLocked=true,Running=false},now,age);
        Assert.True(stale.AccountingConsistent);
        Assert.False(stale.AccountTimestampsFresh);
        Assert.True(stale.HumanReviewRequired);
        Assert.Contains(stale.Findings,x=>x.Code=="RISK_LOCKED");
        Assert.Contains(stale.Findings,x=>x.Code=="LAST_CYCLE_MISSING");
        Assert.Equal("CRITICAL_REVIEW",MiguelAuditHealth.Evaluate(data with {LastCycleUtc=now.AddSeconds(1)},now,age).Status);
        Assert.Null(MiguelAuditHealth.Evaluate(null,now,age).AccountingConsistent);
        Assert.Throws<ArgumentOutOfRangeException>(()=>MiguelAuditHealth.Evaluate(data,now,TimeSpan.Zero));
    }
}
