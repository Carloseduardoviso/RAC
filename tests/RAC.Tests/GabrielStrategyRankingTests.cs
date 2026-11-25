using RAC.Application;
namespace RAC.Tests;
public sealed class GabrielStrategyRankingTests
{
    [Fact]
    public void RanksStressOnlyWithoutTradingAuthorization()
    {
        var p=ResearchExperimentPlan.Build().First().Parameters;
        GabrielExperimentMatrix.Assessment Row(decimal mean,int unresolved,decimal slip)=>new("BTCUSDT",p with {SlippagePctPerSide=slip},
            new(6,mean,1,2,40,20,unresolved,"EXPLORATORY_ONLY"));
        var result=GabrielStrategyRanking.Rank(new[]{Row(9,1,.20m),Row(1,0,.20m),Row(20,0,.05m),Row(-1,0,.20m)});
        Assert.Equal(3,result.Count);
        Assert.Equal("INDEPENDENT_VALIDATION_REQUIRED",result[0].Status);
        Assert.All(result,x=>Assert.False(x.TradingAuthorized));
    }
}
