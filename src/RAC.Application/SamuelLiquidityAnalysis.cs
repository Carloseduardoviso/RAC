namespace RAC.Application;

public sealed record SamuelLiquidityEstimate(decimal SpreadPercent,decimal EntryCost,decimal ExitCost,
    decimal NetResult,decimal NetReturnPercent)
{
    public bool TradingAuthorized=>false;
}
public static class SamuelLiquidityAnalysis
{
    // Hypothetical round trip in quote currency; no order-book depth or execution guarantee.
    public static SamuelLiquidityEstimate Evaluate(decimal bid,decimal ask,decimal quantity,
        decimal expectedExitBid,decimal feePercent,decimal slippagePercent)
    {
        if(bid<=0 || ask<bid || quantity<=0 || expectedExitBid<=0 ||
            feePercent<0 || feePercent>=100 || slippagePercent<0 || slippagePercent>=100)
            throw new ArgumentException("Invalid liquidity inputs.");
        var fee=feePercent/100m;
        var slip=slippagePercent/100m;
        var entry=ask*quantity*(1+slip)*(1+fee);
        var exit=expectedExitBid*quantity*(1-slip)*(1-fee);
        var net=exit-entry;
        return new((ask-bid)/ask*100m,entry-ask*quantity,expectedExitBid*quantity-exit,net,net/entry*100m);
    }
}
