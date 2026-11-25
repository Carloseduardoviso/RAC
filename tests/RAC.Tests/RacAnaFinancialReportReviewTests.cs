using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class RacAnaFinancialReportReviewTests
{
    [Fact]
    public void PreservesFinancialEvidenceAndRejectsTampering()
    {
        var evidence=Guid.NewGuid();
        var report=CarlosFinancialAdvisoryReport.Create(Guid.NewGuid(),
            new("RISK_LOCKED",[evidence],["MIGUEL:RISK_LOCKED"]),DateTimeOffset.UtcNow);
        var result=RacAnaFinancialReportReview.Review(report);
        Assert.Equal("RISK_LOCKED",result.Status);
        Assert.Equal(evidence,Assert.Single(result.EvidenceIds));
        Assert.False(result.TradingAuthorized);
        Assert.Equal("INVALID_FINANCIAL_REPORT",RacAnaFinancialReportReview.Review(report with {Kind=RacMessageKind.Report}).Status);
        Assert.Equal("INVALID_FINANCIAL_REPORT",RacAnaFinancialReportReview.Review(report with {Content=report.Content.Replace("\"TradingAuthorized\":false","\"TradingAuthorized\":true")}).Status);
        Assert.Equal("INVALID_FINANCIAL_REPORT",RacAnaFinancialReportReview.Review(report with {Content="{}"}).Status);
    }
}
