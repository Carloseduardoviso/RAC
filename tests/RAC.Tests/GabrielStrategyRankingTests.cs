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
    [Fact]
    public void FreezeCandidatesRequireCompletePositiveStressEvidence()
    {
        var p=ResearchExperimentPlan.Build().First().Parameters with {SlippagePctPerSide=.20m};
        GabrielExperimentMatrix.Assessment Row(string symbol,int months,decimal mean,int trades,int unresolved,decimal slip)=>new(symbol,p with {SlippagePctPerSide=slip},new(months,mean,1,2,trades,0,unresolved,"EXPLORATORY_ONLY"));
        var eligible=Row("BTCUSDT",6,1,30,0,.20m);
        var rows=new[]{eligible,eligible,Row("ETHUSDT",5,1,30,0,.20m),Row("SOLUSDT",6,0,30,0,.20m),Row("BNBUSDT",6,1,29,0,.20m),Row("ADAUSDT",6,1,30,1,.20m),Row("XRPUSDT",6,1,30,0,.05m)};
        var candidate=Assert.Single(GabrielStrategyRanking.FreezeCandidates(rows));
        Assert.Equal(new ResearchExperimentPlan.Row(eligible.Symbol,eligible.Parameters),candidate);
        Assert.Equal(candidate,Assert.Single(GabrielStrategyRanking.FreezeCandidates(rows.Reverse().ToArray())));
    }
    [Fact]
    public void FreezePairsRequireMatchingCompleteNormalCostEvidence()
    {
        var p=ResearchExperimentPlan.Build().First().Parameters with {SlippagePctPerSide=.20m};
        var stress=new GabrielExperimentMatrix.Assessment("BTCUSDT",p,new(6,1,1,2,30,20,0,"EXPLORATORY_ONLY"));
        var normal=stress with {Parameters=p with {SlippagePctPerSide=.05m}};
        Assert.Throws<InvalidOperationException>(()=>GabrielStrategyRanking.FreezePairs(new[]{stress}));
        Assert.Throws<InvalidOperationException>(()=>GabrielStrategyRanking.FreezePairs(new[]{stress,normal,normal}));
        var pair=GabrielStrategyRanking.FreezePairs(new[]{stress,normal});
        Assert.Equal(2,pair.Count);
        Assert.Equal(p,pair[0].Parameters);
        Assert.Equal(normal.Parameters,pair[1].Parameters);
        Assert.Empty(GabrielStrategyRanking.FreezePairs(new[]{normal}));
    }
}
