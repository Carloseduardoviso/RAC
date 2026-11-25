using RAC.Application;
namespace RAC.Tests;

public sealed class RafaelCapitalAnalysisTests
{
    [Fact]
    public void CalculatesReadOnlyCapitalAndPreservesRiskLock()
    {
        var result=RafaelCapitalAnalysis.Evaluate(new(80m,20m,110m,true));
        Assert.Equal(100m,result.Equity);
        Assert.Equal(20m,result.ExposurePercent);
        Assert.Equal(80m,result.CashPercent);
        Assert.Equal(-10m,result.NetResult);
        Assert.Equal("RISK_LOCKED",result.Status);
        Assert.False(result.TradingAuthorized);
        var empty=RafaelCapitalAnalysis.Evaluate(new(0,0,0,false));
        Assert.Null(empty.ExposurePercent);
        Assert.Equal("NO_CAPITAL",empty.Status);
        Assert.False(RafaelCapitalAnalysis.Evaluate(new(100,0,100,false)).TradingAuthorized);
        Assert.Throws<ArgumentException>(()=>RafaelCapitalAnalysis.Evaluate(new(-1,0,0,false)));
    }
}
