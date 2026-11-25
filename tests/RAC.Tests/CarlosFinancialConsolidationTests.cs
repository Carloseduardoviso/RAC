using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class CarlosFinancialConsolidationTests
{
    [Fact]
    public void ConsolidatesCapitalWithoutAuthorizingTrading()
    {
        var now=DateTimeOffset.UtcNow;
        var data=new RafaelStoredCapital(new(80,20,90,false),now,now,true,1,2,5,100);
        var result=CarlosFinancialConsolidation.Evaluate(data,now,TimeSpan.FromMinutes(2),0.01m);
        Assert.Equal(100m,result.Capital!.Equity);
        Assert.Equal(20m,result.Capital.ExposurePercent);
        Assert.Equal(10m,result.Capital.NetResult);
        Assert.Equal(5m,result.RealizedNetProfit);
        Assert.Equal("HUMAN_REVIEW_REQUIRED",result.Status);
        Assert.False(result.TradingAuthorized);
    }
    [Fact]
    public void PreservesRiskLockAndFlagsInconsistentOrMissingData()
    {
        var now=DateTimeOffset.UtcNow;
        var data=new RafaelStoredCapital(new(80,20,100,true),now.AddMinutes(-5),null,false,1,2,0,101);
        var result=CarlosFinancialConsolidation.Evaluate(data,now,TimeSpan.FromMinutes(2),0.01m);
        Assert.Equal("RISK_LOCKED",result.Status);
        Assert.Contains("EQUITY_MISMATCH",result.Findings);
        Assert.Contains("ACCOUNT_TIMESTAMP_INVALID_OR_STALE",result.Findings);
        Assert.Contains("PAPER_STOPPED",result.Findings);
        Assert.False(result.TradingAuthorized);
        var missing=CarlosFinancialConsolidation.Evaluate(null,now,TimeSpan.FromMinutes(2),0);
        Assert.Null(missing.Capital);
        Assert.Contains("NO_PAPER_ACCOUNT",missing.Findings);
        Assert.False(missing.TradingAuthorized);
    }
}
