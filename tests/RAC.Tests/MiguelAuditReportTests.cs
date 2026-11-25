using RAC.Application;
using RAC.Infrastructure;
using System.Text.Json;
namespace RAC.Tests;
public sealed class MiguelAuditReportTests
{
    [Fact]
    public void ReportsRiskAndMissingDataWithoutAuthorizingTrading()
    {
        var now=DateTimeOffset.UtcNow;
        var id=Guid.NewGuid();
        var audit=MiguelIntegrityAudit.Evaluate(99,100,100,[],[],0.01m,0);
        var data=new MiguelStoredAudit(audit,true,false,now,now,0,0);
        var report=MiguelAuditReport.Create(id,data,now,TimeSpan.FromMinutes(2));
        Assert.Equal("miguel",report.Sender);
        Assert.Equal("carlos",report.Recipient);
        Assert.Equal(id,report.CorrelationId);
        Assert.Equal(report,MiguelAuditReport.Create(id,data,now,TimeSpan.FromMinutes(2)));
        using var json=JsonDocument.Parse(report.Content);
        Assert.False(json.RootElement.GetProperty("TradingAuthorized").GetBoolean());
        Assert.True(json.RootElement.GetProperty("HumanReviewRequired").GetBoolean());
        Assert.True(json.RootElement.GetProperty("RiskLocked").GetBoolean());
        Assert.Equal("CRITICAL_REVIEW",json.RootElement.GetProperty("Status").GetString());
        using var missing=JsonDocument.Parse(MiguelAuditReport.Create(Guid.NewGuid(),null,now,TimeSpan.FromMinutes(2)).Content);
        Assert.Equal(JsonValueKind.Null,missing.RootElement.GetProperty("CalculatedEquity").ValueKind);
        Assert.Equal("NO_PAPER_ACCOUNT",missing.RootElement.GetProperty("Status").GetString());
    }
}
