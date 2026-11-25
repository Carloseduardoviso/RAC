using RAC.Application;
namespace RAC.Tests;
public sealed class GabrielHistoricalAnalysisTests
{
    [Fact]
    public void PreservesIndependentMonthlyReplayEvidence()
    {
        var rows=new[]{new GabrielMonthlyReplay("2026-05",new(2m,5m,10,6,2m,1m,false)),
            new GabrielMonthlyReplay("2026-06",new(-1m,3m,8,3,0.5m,1m,true))};
        var result=GabrielHistoricalAnalysis.Evaluate(rows);
        Assert.Equal(.5m,result.MeanNetPercent);
        Assert.Equal(5m,result.WorstMonthlyDrawdownPercent);
        Assert.Equal(18,result.Trades);
        Assert.Equal(9,result.Wins);
        Assert.Equal("UNRESOLVED_POSITIONS",result.Status);
        Assert.False(result.TradingAuthorized);
        Assert.Throws<ArgumentException>(()=>GabrielHistoricalAnalysis.Evaluate(new[]{rows[0],rows[0]}));
    }
}
