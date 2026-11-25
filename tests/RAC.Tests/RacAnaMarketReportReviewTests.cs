using RAC.Application;
using RAC.Infrastructure;
using Xunit;
namespace RAC.Tests;
public sealed class RacAnaMarketReportReviewTests
{
    [Theory]
    [InlineData("CRITICAL_REVIEW","RISK_REVIEW_REQUIRED")]
    [InlineData("INCOMPLETE_EVIDENCE","INCOMPLETE_REVIEW")]
    [InlineData("HUMAN_REVIEW_REQUIRED","HUMAN_REVIEW_REQUIRED")]
    public void AnaClassifiesVersionedRebecaMarketReports(string marketStatus,string expected)
    {
        var now=DateTimeOffset.UtcNow; var correlation=Guid.NewGuid(); var original=Guid.NewGuid();
        var report=RebecaMarketAdvisoryReport.Create(correlation,new(marketStatus,[original],["NEWS:BTCUSDT:HIGH_IMPACT_REVIEW"],[]),now);
        var carlos=RacAgentCommunication.Create("carlos","ana",RacMessageKind.Report,"financial evidence",correlation,now);
        var lucas=RacAgentCommunication.Create("lucas","ana",RacMessageKind.Report,"health evidence",correlation,now);
        var review=RacAnaReportReview.Review(correlation,[report,carlos,lucas],now,TimeSpan.FromMinutes(5));
        var market=Assert.Single(review.MarketReports);
        Assert.Equal(marketStatus,market.Status);
        var summary=RacAnaAdvisory.Summarize([review]);
        Assert.Equal(expected,summary.Status);
        Assert.Contains(original,summary.EvidenceIds);
        Assert.Contains(report.Id,summary.EvidenceIds);
        Assert.False(summary.TradingAuthorized);
    }
    [Theory]
    [InlineData("\"TradingAuthorized\":false","\"TradingAuthorized\":true")]
    [InlineData("\"HumanReviewRequired\":true","\"HumanReviewRequired\":false")]
    [InlineData("\"Schema\":\"REBECA_MARKET_V1\"","\"Schema\":\"UNKNOWN\"")]
    public void InvalidPermissionOrSchemaFailsClosed(string original,string replacement)
    {
        var now=DateTimeOffset.UtcNow; var correlation=Guid.NewGuid(); var evidence=Guid.NewGuid();
        var report=RebecaMarketAdvisoryReport.Create(correlation,new("HUMAN_REVIEW_REQUIRED",[evidence],["RESEARCH_ONLY"],[]),now);
        var invalid=report with {Content=report.Content.Replace(original,replacement)};
        var review=RacAnaReportReview.Review(correlation,[invalid],now,TimeSpan.FromMinutes(5));
        Assert.Equal("INVALID_REPORT",Assert.Single(review.MarketReports).Status);
        Assert.Equal("RISK_REVIEW_REQUIRED",RacAnaAdvisory.Summarize([review]).Status);
    }
    [Fact]
    public void MalformedJsonFailsClosed()
    {
        var now=DateTimeOffset.UtcNow; var correlation=Guid.NewGuid();
        var valid=RebecaMarketAdvisoryReport.Create(correlation,new("HUMAN_REVIEW_REQUIRED",[Guid.NewGuid()],["RESEARCH_ONLY"],[]),now);
        var invalid=valid with {Content="{not-json"};
        var result=RacAnaMarketReportReview.Review(invalid);
        Assert.Equal("INVALID_REPORT",result.Status);
        Assert.Contains("MALFORMED_MARKET_JSON",result.Reasons);
    }
}
