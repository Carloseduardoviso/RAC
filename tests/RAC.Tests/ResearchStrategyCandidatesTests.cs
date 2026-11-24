using RAC.Infrastructure;
namespace RAC.Tests;
public class ResearchStrategyCandidatesTests
{
    [Fact]
    public void FailedStrategyMentionOnlyCreatesUnvalidatedHypothesis()
    {
        var permission=ResearchAbstractUsagePolicy.Evaluate("https://creativecommons.org/publicdomain/zero/1.0/",true);
        var observations=ResearchContextAnalysis.Analyze("Mean reversion failed after transaction costs.",permission);
        var candidate=Assert.Single(ResearchStrategyCandidates.Classify("10.1234/a",observations));
        Assert.Equal("MEAN_REVERSION_V1",candidate.TemplateId);
        Assert.Equal("HYPOTHESIS_UNVALIDATED",candidate.Status);
        Assert.False(candidate.TradingAuthorized);
    }
    [Fact]
    public void RejectsInvalidDoiAndUnverifiedContent()
    {
        var observations=ResearchContextAnalysis.Analyze("mean reversion",ResearchAbstractUsagePolicy.Evaluate(null,false));
        Assert.Empty(ResearchStrategyCandidates.Classify("10.1234/a",observations));
        Assert.Empty(ResearchStrategyCandidates.Classify("invalid",observations));
    }
}
