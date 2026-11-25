using RAC.Application;
namespace RAC.Tests;
public sealed class SamuelLiquidityAnalysisTests
{
    [Fact]
    public void IncludesBothSidesAndRejectsInvalidQuotes()
    {
        var result=SamuelLiquidityAnalysis.Evaluate(99,100,1,99,1,1);
        Assert.Equal(1m,result.SpreadPercent);
        Assert.Equal(2.01m,result.EntryCost);
        Assert.Equal(1.9701m,result.ExitCost);
        Assert.Equal(-4.9801m,result.NetResult);
        Assert.False(result.TradingAuthorized);
        Assert.Equal(10m,SamuelLiquidityAnalysis.Evaluate(100,100,1,110,0,0).NetReturnPercent);
        Assert.Throws<ArgumentException>(()=>SamuelLiquidityAnalysis.Evaluate(101,100,1,110,0,0));
        Assert.Throws<ArgumentException>(()=>SamuelLiquidityAnalysis.Evaluate(99,100,1,110,0,100));
    }
}
