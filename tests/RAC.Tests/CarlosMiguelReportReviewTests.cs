using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class CarlosMiguelReportReviewTests
{
    [Fact]
    public void PreservesRiskAndRejectsInconsistentFindingCounts()
    {
        var now=DateTimeOffset.UtcNow;
        var audit=MiguelIntegrityAudit.Evaluate(99,100,100,[],[],0.01m,0);
        var data=new MiguelStoredAudit(audit,true,false,now,now,0,0);
        var report=MiguelAuditReport.Create(Guid.NewGuid(),data,now,TimeSpan.FromMinutes(2));
        var evidence=new CarlosMiguelEvidence(report.CorrelationId,report.Id,now,report.Content);
        var result=CarlosMiguelReportReview.Review(evidence);
        Assert.Equal("RISK_LOCKED",result.Status);
        Assert.Contains("WARNING:RISK_LOCKED",result.Findings);
        Assert.False(result.TradingAuthorized);
        Assert.Equal("INVALID_REPORT",CarlosMiguelReportReview.Review(evidence with {Content=report.Content.Replace("\"HumanReviewRequired\":true","\"HumanReviewRequired\":false")}).Status);
        var missing=MiguelAuditReport.Create(Guid.NewGuid(),null,now,TimeSpan.FromMinutes(2));
        Assert.Equal("CRITICAL_REVIEW",CarlosMiguelReportReview.Review(new(missing.CorrelationId,missing.Id,now,missing.Content)).Status);
    }
}
