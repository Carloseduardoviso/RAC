using RAC.Application;
namespace RAC.Tests;
public sealed class MiguelMovementReconciliationTests
{
    [Fact]
    public void ReconcilesFullPaperTradesAndRejectsIncompleteHistory()
    {
        var now=DateTimeOffset.UtcNow;
        var buy=new MiguelFill(1,"BTCUSDT","BUY",2,10,1,null,now);
        var sell=new MiguelFill(2,"BTCUSDT","SELL",2,12,1,2,now.AddSeconds(1));
        var valid=MiguelMovementReconciliation.Evaluate([sell,buy],100,102,0.01m);
        Assert.Empty(valid.Findings);
        Assert.Equal(102m,valid.ExpectedCash);
        Assert.Equal(2m,valid.CalculatedRealized);
        Assert.Equal(2m,valid.TotalFees);
        Assert.False(valid.TradingAuthorized);
        var wrong=MiguelMovementReconciliation.Evaluate([buy,sell with {Realized=4}],100,100,0.01m);
        Assert.Contains("REALIZED_MISMATCH:2",wrong.Findings);
        Assert.Contains("CASH_MISMATCH",wrong.Findings);
        var incomplete=MiguelMovementReconciliation.Evaluate([sell],100,102,0.01m);
        Assert.Null(incomplete.ExpectedCash);
        Assert.Contains("UNMATCHED_SELL:2",incomplete.Findings);
        Assert.Null(MiguelMovementReconciliation.Evaluate([buy,buy],100,79,0.01m).CalculatedRealized);
    }
}
