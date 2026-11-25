using RAC.Application;
namespace RAC.Tests;

public sealed class GabrielPerformanceAnalysisTests
{
    [Fact]
    public void CalculatesOrderedNetReturnsAndDrawdown()
    {
        var result=GabrielPerformanceAnalysis.Calculate(new[]{10m,-10m});
        Assert.Equal(0m,result.MeanPercent);
        Assert.Equal(1m,result.ProfitFactor);
        Assert.Equal(-1d,result.CompoundedReturnPercent!.Value,8);
        Assert.Equal(10d,result.MaxDrawdownPercent!.Value,8);
        Assert.Equal(Math.Sqrt(200),result.SampleVolatilityPercent!.Value,8);
        Assert.False(result.TradingAuthorized);
        Assert.Null(GabrielPerformanceAnalysis.Calculate(new[]{1m}).ProfitFactor);
        Assert.Null(GabrielPerformanceAnalysis.Calculate(Array.Empty<decimal>()).MaxDrawdownPercent);
        Assert.Equal(100d,GabrielPerformanceAnalysis.Calculate(new[]{-100m,10m}).MaxDrawdownPercent!.Value,8);
        Assert.Throws<ArgumentException>(()=>GabrielPerformanceAnalysis.Calculate(new[]{-101m}));
    }
}
