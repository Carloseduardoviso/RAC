namespace RAC.Application;
public sealed record SamuelRoundTripEstimate(SamuelDepthEstimate Entry,SamuelDepthEstimate Exit,
    decimal? TotalEntry,decimal? NetExit,decimal? NetResult,decimal? NetReturnPercent,
    decimal? BreakEvenExitAverage,string Status)
{
    public bool TradingAuthorized=>false;
}
public static class SamuelRoundTrip
{
    // Same snapshot on both sides; hypothetical liquidation, not a forecast.
    public static SamuelRoundTripEstimate Evaluate(SamuelDepthSnapshot book,decimal quantity,
        decimal feePercent,DateTimeOffset now)
    {
        if(feePercent<0 || feePercent>=100 || book.ReceivedUtc>now ||
            now-book.ReceivedUtc>TimeSpan.FromSeconds(30) || book.Bids.Count==0 ||
            book.Asks.Count==0 || book.Bids[0].Price>=book.Asks[0].Price)
            throw new ArgumentException("Invalid round trip inputs.");
        var entry=SamuelBookDepth.Evaluate(book.Asks,quantity,true);
        var exit=SamuelBookDepth.Evaluate(book.Bids,quantity,false);
        if(!entry.FullyCovered || !exit.FullyCovered)
            return new(entry,exit,null,null,null,null,null,"INSUFFICIENT_DEPTH");
        var fee=feePercent/100m;
        var total=entry.Notional*(1+fee);
        var proceeds=exit.Notional*(1-fee);
        var net=proceeds-total;
        return new(entry,exit,total,proceeds,net,net/total*100m,
            total/(quantity*(1-fee)),"RESEARCH_ONLY");
    }
}
