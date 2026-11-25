using RAC.Application;
using RAC.Infrastructure;
using System.Text.Json;
namespace RAC.Tests;
public sealed class CarlosFinancialAdvisoryReportTests
{
    [Theory]
    [InlineData("RISK_LOCKED",RacMessageKind.RiskNotice)]
    [InlineData("CRITICAL_REVIEW",RacMessageKind.RiskNotice)]
    [InlineData("REVIEW_REQUIRED",RacMessageKind.Report)]
    [InlineData("HUMAN_REVIEW_REQUIRED",RacMessageKind.Report)]
    public void RoutesAdvisoryWithoutTradingAuthorization(string status,RacMessageKind kind)
    {
        var id=Guid.NewGuid(); var evidence=Guid.NewGuid(); var now=DateTimeOffset.UtcNow;
        var result=new CarlosFinancialEvidenceResult(status,[evidence],["CROSS_CHECK:EQUITY_MISMATCH"]);
        var report=CarlosFinancialAdvisoryReport.Create(id,result,now);
        Assert.Equal("carlos",report.Sender);
        Assert.Equal("ana",report.Recipient);
        Assert.Equal(kind,report.Kind);
        Assert.Equal(id,report.CorrelationId);
        Assert.Equal(report.Id,CarlosFinancialAdvisoryReport.Create(id,result,now).Id);
        using var json=JsonDocument.Parse(report.Content);
        Assert.False(json.RootElement.GetProperty("TradingAuthorized").GetBoolean());
        Assert.True(json.RootElement.GetProperty("HumanReviewRequired").GetBoolean());
        Assert.Equal(evidence,json.RootElement.GetProperty("EvidenceIds")[0].GetGuid());
        Assert.Equal(status,json.RootElement.GetProperty("Status").GetString());
    }
    [Fact]
    public void RejectsOversizedAdvisoryWithoutDroppingEvidence()
    {
        var ids=Enumerable.Range(0,150).Select(_=>Guid.NewGuid()).ToArray();
        var result=new CarlosFinancialEvidenceResult("RISK_LOCKED",ids,["MIGUEL:RISK_LOCKED"]);
        Assert.Throws<InvalidOperationException>(()=>CarlosFinancialAdvisoryReport.Create(Guid.NewGuid(),result,DateTimeOffset.UtcNow));
        Assert.Equal(150,result.EvidenceIds.Count);
        Assert.False(result.TradingAuthorized);
        Assert.Throws<ArgumentException>(()=>CarlosFinancialAdvisoryReport.Create(Guid.NewGuid(),new("RISK_LOCKED",[],["TEST"]),DateTimeOffset.UtcNow));
    }

}
