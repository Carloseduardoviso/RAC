using RAC.Application;
namespace RAC.Tests;
public sealed class MiguelIntegrityAuditTests
{
    [Fact]
    public void ConsolidatesRealizedAndUnrealizedProfitAndFlagsDivergence()
    {
        var now=DateTimeOffset.UtcNow;
        MiguelFill[] fills=[new(1,"BTCUSDT","BUY",2,10,1,null,now),
            new(2,"BTCUSDT","SELL",2,12,1,2,now.AddSeconds(1)),
            new(3,"ETHUSDT","BUY",1,20,1,null,now.AddSeconds(2))];
        MiguelOpenPosition[] positions=[new("ETHUSDT",1,20,1,24)];
        var valid=MiguelIntegrityAudit.Evaluate(81,100,105,fills,positions,0.01m,0.000001m);
        Assert.True(valid.Consistent);
        Assert.Equal(0m,valid.ProfitDifference);
        Assert.Equal(5m,valid.Equity.NetResult);
        Assert.False(valid.TradingAuthorized);
        var wrong=MiguelIntegrityAudit.Evaluate(80,100,105,fills,positions,0.01m,0.000001m);
        Assert.False(wrong.Consistent);
        Assert.Contains(wrong.Findings,f=>f.Code=="PROFIT_RECONCILIATION_MISMATCH" && f.Severity=="CRITICAL");
        var missing=MiguelIntegrityAudit.Evaluate(81,100,105,fills,[],0.01m,0);
        Assert.Null(missing.ProfitDifference);
        Assert.Contains(missing.Findings,f=>f.Code=="PROFIT_RECONCILIATION_UNAVAILABLE");
    }
}
