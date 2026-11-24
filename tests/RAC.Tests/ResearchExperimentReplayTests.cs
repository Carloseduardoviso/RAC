using RAC.Application;
namespace RAC.Tests;
public class ResearchExperimentReplayTests
{
    [Fact]
    public void FlatMarketDoesNotCreateProfitOrTrades()
    {
        var data=Enumerable.Repeat(new ResearchExperimentReplay.Minute(100,100,1000000),10000).ToArray();
        foreach(var row in ResearchExperimentPlan.Build().Take(24))
        {
            var r=ResearchExperimentReplay.Run(data,row.Parameters);
            Assert.Equal(0,r.NetPct);Assert.Equal(0,r.Trades);Assert.False(r.Unresolved);
        }
    }
    [Fact]
    public void RejectsTradingAuthorization()
    {
        var p=ResearchExperimentPlan.Build()[0].Parameters with {TradingAuthorized=true};
        Assert.Throws<ArgumentException>(()=>ResearchExperimentReplay.Run([],p));
    }
}
