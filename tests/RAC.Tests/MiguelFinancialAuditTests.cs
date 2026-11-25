using RAC.Application;
namespace RAC.Tests;
public sealed class MiguelFinancialAuditTests
{
    [Fact]
    public void ReconcilesEquityAndReportsAnomaliesWithoutAuthorizingTrading()
    {
        var valid=MiguelFinancialAudit.Evaluate(80,[12,8],110,100,0.01m);
        Assert.True(valid.Consistent);
        Assert.Equal(100m,valid.CalculatedEquity);
        Assert.Equal(-10m,valid.NetResult);
        Assert.False(valid.TradingAuthorized);
        Assert.True(MiguelFinancialAudit.Evaluate(80,[20],100,100.01m,0.01m).Consistent);
        var invalid=MiguelFinancialAudit.Evaluate(-1,[-2],100,90,0.01m);
        Assert.False(invalid.Consistent);
        Assert.Equal(-93m,invalid.Difference);
        Assert.Contains("NEGATIVE_CASH",invalid.Findings);
        Assert.Contains("NEGATIVE_POSITION_VALUE",invalid.Findings);
        Assert.Contains("EQUITY_MISMATCH",invalid.Findings);
        Assert.Throws<ArgumentException>(()=>MiguelFinancialAudit.Evaluate(0,[],0,0,-1));
    }
}
