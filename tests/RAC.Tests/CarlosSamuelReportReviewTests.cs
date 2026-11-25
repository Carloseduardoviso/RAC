using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class CarlosSamuelReportReviewTests
{
    [Fact]
    public void ReviewsProducerAndRejectsTamperedScenario()
    {
        var now=DateTimeOffset.UtcNow;
        var book=new SamuelDepthSnapshot(1,now,[new(99,1)],[new(100,1)]);
        var report=SamuelLiquidityReport.Create(Guid.NewGuid(),"BTCUSDT",book,[1,2],1,2,2,now);
        var evidence=new CarlosSamuelEvidence(report.CorrelationId,report.Id,now,report.Content);
        var result=CarlosSamuelReportReview.Review(evidence);
        Assert.Equal("REVIEW_REQUIRED",result.Status);
        Assert.Contains("EXCESSIVE_COST",result.Findings);
        Assert.Contains("INSUFFICIENT_DEPTH",result.Findings);
        Assert.False(result.TradingAuthorized);
        Assert.Equal("INVALID_REPORT",CarlosSamuelReportReview.Review(evidence with {Content=report.Content.Replace("EXCESSIVE_COST","RESEARCH_ONLY")}).Status);
        Assert.Equal("INVALID_REPORT",CarlosSamuelReportReview.Review(evidence with {ObservedUtc=now.AddMinutes(1)}).Status);
    }
}
