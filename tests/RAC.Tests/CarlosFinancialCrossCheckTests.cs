using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class CarlosFinancialCrossCheckTests
{
    [Fact]
    public void ComparesMatchingSnapshotsAndRejectsDifferentSnapshots()
    {
        var now=DateTimeOffset.UtcNow;
        var r=RafaelFinancialReport.Create(Guid.NewGuid(),now,new(new(100,0,100,false),now,now,true,0,0,0,100),
            new(30,40,15),TimeSpan.FromMinutes(2),0.01m);
        var audit=MiguelIntegrityAudit.Evaluate(99,100,100,[],[],0.01m,0);
        var m=MiguelAuditReport.Create(Guid.NewGuid(),new(audit,true,false,now,now,0,0),now,TimeSpan.FromMinutes(2));
        var re=new CarlosRafaelEvidence(r.CorrelationId,r.Id,now,r.Content);
        var me=new CarlosMiguelEvidence(m.CorrelationId,m.Id,now,m.Content);
        var findings=CarlosFinancialCrossCheck.Review(re,me,0.01m);
        Assert.Contains("EQUITY_MISMATCH",findings);
        Assert.Contains("RISK_STATE_MISMATCH",findings);
        var later=MiguelAuditReport.Create(Guid.NewGuid(),new(audit,true,false,now.AddSeconds(-1),now.AddSeconds(-1),0,0),now,TimeSpan.FromMinutes(2));
        Assert.Equal(new[]{"SNAPSHOT_NOT_COMPARABLE"},CarlosFinancialCrossCheck.Review(re,new(later.CorrelationId,later.Id,now,later.Content),0.01m));
    }
}
