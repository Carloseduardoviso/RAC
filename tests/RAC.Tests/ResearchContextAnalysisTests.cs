using RAC.Infrastructure;
namespace RAC.Tests;
public class ResearchContextAnalysisTests
{
    [Fact]
    public void DeniesUnverifiedContent()
    {
        Assert.Empty(ResearchContextAnalysis.Analyze("mean reversion",ResearchAbstractUsagePolicy.Evaluate(null,false)));
    }
    [Fact]
    public void NegativeResultsRemainMentionsNotRecommendations()
    {
        var permission=ResearchAbstractUsagePolicy.Evaluate("https://creativecommons.org/publicdomain/zero/1.0/",true);
        var result=ResearchContextAnalysis.Analyze("Mean reversion failed after transaction costs. Overfitting was observed.",permission);
        Assert.Equal(3,result.Count);
        Assert.All(result,x=>{Assert.Equal("ABSTRACT_MENTION",x.Level);Assert.False(x.TradingAuthorized);});
    }
}
