using RAC.Application;
namespace RAC.Tests;
public sealed class MiguelPositionReconciliationTests
{
    [Fact]
    public void MatchesOpenPositionsAndSuppressesUnreconciledTotals()
    {
        var buy=new MiguelFill(1,"BTCUSDT","BUY",2,10,1,null,DateTimeOffset.UtcNow);
        var pos=new MiguelOpenPosition("BTCUSDT",2,10,1,12);
        var result=MiguelPositionReconciliation.Evaluate([buy],[pos],0.01m,0.000001m);
        Assert.Empty(result.Findings);
        Assert.Equal(21m,result.CostBasis);
        Assert.Equal(24m,result.MarkedValue);
        Assert.Equal(3m,result.Unrealized);
        Assert.False(result.TradingAuthorized);
        var wrong=MiguelPositionReconciliation.Evaluate([buy],[pos with {Quantity=3,EntryFee=2}],0.01m,0.000001m);
        Assert.Contains("QUANTITY_MISMATCH:BTCUSDT",wrong.Findings);
        Assert.Contains("ENTRY_FEE_MISMATCH:BTCUSDT",wrong.Findings);
        Assert.Null(wrong.Unrealized);
        Assert.Contains("MISSING_POSITION:BTCUSDT",MiguelPositionReconciliation.Evaluate([buy],[],0,0).Findings);
        Assert.Contains("POSITION_WITHOUT_BUY:BTCUSDT",MiguelPositionReconciliation.Evaluate([],[pos],0,0).Findings);
        Assert.Contains("INCOMPLETE_FILL_HISTORY",MiguelPositionReconciliation.Evaluate([buy with {Side="SELL"}],[],0,0).Findings);
    }
}
