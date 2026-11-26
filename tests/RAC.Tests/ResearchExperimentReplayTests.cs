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
    [Fact]
    public void DetailedReplayPreservesHistoricalSummaryAndExposesRealizedMetrics()
    {
        var data=Enumerable.Range(0,43200).Select(i=>
        {
            decimal price=(i/60)%24<12?100+(i/60)%24:112-((i/60)%24-12);
            return new ResearchExperimentReplay.Minute(price,price,1000000);
        }).ToArray();
        var traded=false;
        foreach(var row in ResearchExperimentPlan.Build().Take(24))
        {
            var detail=ResearchExperimentReplay.RunDetailed(data,row.Parameters);
            Assert.Equal(ResearchExperimentReplay.Run(data,row.Parameters),detail.Summary);
            Assert.True(detail.RealizedProfit>=0);
            Assert.True(detail.RealizedLoss>=0);
            Assert.Equal(detail.RealizedLoss>0?detail.RealizedProfit/detail.RealizedLoss:(decimal?)null,detail.Summary.ProfitFactor);
            if(detail.Summary.Trades>0)traded=true;
        }
        Assert.True(traded);
    }
}
