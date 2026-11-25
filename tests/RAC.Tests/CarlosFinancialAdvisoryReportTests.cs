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
        Assert.Throws<CarlosFinancialCapacityException>(()=>CarlosFinancialAdvisoryReport.Create(Guid.NewGuid(),result,DateTimeOffset.UtcNow));
        Assert.Equal(150,result.EvidenceIds.Count);
        Assert.False(result.TradingAuthorized);
        Assert.Throws<ArgumentException>(()=>CarlosFinancialAdvisoryReport.Create(Guid.NewGuid(),new("RISK_LOCKED",[],["TEST"]),DateTimeOffset.UtcNow));
    }

    [Fact]
    public void OversizedPublicationProducesExplicitRiskNotice()
    {
        var ids=Enumerable.Range(0,150).Select(_=>Guid.NewGuid()).ToArray();
        var result=new CarlosFinancialEvidenceResult("RISK_LOCKED",ids,["MIGUEL:RISK_LOCKED"]);
        var correlation=Guid.NewGuid(); var r=Guid.NewGuid(); var s=Guid.NewGuid(); var m=Guid.NewGuid();
        var now=DateTimeOffset.UtcNow;
        var report=CarlosFinancialAdvisoryReport.CreateForPublication(correlation,result,r,s,m,now);
        Assert.Equal(RacMessageKind.RiskNotice,report.Kind);
        Assert.True(report.Content.Length<=4096);
        Assert.Equal(report.Id,CarlosFinancialAdvisoryReport.CreateForPublication(correlation,result,r,s,m,now).Id);
        using var json=JsonDocument.Parse(report.Content);
        Assert.Equal(150,json.RootElement.GetProperty("EvidenceCount").GetInt32());
        Assert.Equal(r,json.RootElement.GetProperty("RafaelTask").GetGuid());
        Assert.False(json.RootElement.GetProperty("TradingAuthorized").GetBoolean());
        Assert.Contains("CAPACITY_EXCEEDED",report.Content);
        // Capacity notices are recognized but never authorize trading.
        Assert.Equal("CRITICAL_REVIEW",RacAnaFinancialReportReview.Review(report).Status);
        Assert.Empty(RacAnaFinancialReportReview.Review(report).EvidenceIds);
        Assert.Equal("INVALID_FINANCIAL_REPORT",RacAnaFinancialReportReview.Review(report with {Content=report.Content.Replace(r.ToString(),Guid.Empty.ToString())}).Status);
    }
}
