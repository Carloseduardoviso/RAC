using RAC.Application;
namespace RAC.Tests;
public class ResearchLeadershipReviewTests
{
    [Fact]
    public void RejectsNegativeFullPeriodEvenWithPositiveEvaluation()
    {
        var r=ResearchLeadershipReview.Evaluate(new(-.33m,.259m,2,47,1.47m,0));
        Assert.Equal("REJECT_CURRENT_CONFIGURATION",r.Ana);
        Assert.Contains("NONPOSITIVE_NET_RETURN",r.Reasons);
        Assert.False(r.TradingAuthorized);
    }
    [Fact]
    public void PassingScreenOnlyQueuesIndependentResearch()
    {
        var r=ResearchLeadershipReview.Evaluate(new(1,1,5,40,2,0));
        Assert.Equal("QUEUE_INDEPENDENT_RESEARCH",r.Ana);
        Assert.False(r.TradingAuthorized);
    }
}
