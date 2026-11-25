using RAC.Application;
using Xunit;

namespace RAC.Tests;
public sealed class RacAnaAdvisoryTests
{
    [Fact]
    public void RiskNoticeTakesPrecedenceAndNeverAuthorizesTrading()
    {
        var id = Guid.NewGuid();
        var review = new RacAnaReportReviewResult(id, [], ["lucas"], ["rebeca"], true);
        var result = RacAnaAdvisory.Summarize([review]);
        Assert.Equal("RISK_REVIEW_REQUIRED",result.Status);
        Assert.Equal(3,result.Reasons.Count);
        Assert.False(result.TradingAuthorized);
    }
    [Fact]
    public void EmptyOrCompleteEvidenceStillRequiresReview()
    {
        var empty = RacAnaAdvisory.Summarize([]);
        Assert.Equal("INCOMPLETE_REVIEW",empty.Status);
        var complete = RacAnaAdvisory.Summarize([new(Guid.NewGuid(),[],[],[],false)]);
        Assert.Equal("HUMAN_REVIEW_REQUIRED",complete.Status);
        Assert.False(empty.TradingAuthorized);
        Assert.False(complete.TradingAuthorized);
    }
}
