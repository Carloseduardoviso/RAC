namespace RAC.Application;
public sealed record SamuelBookLevel(decimal Price, decimal Quantity);
public sealed record SamuelDepthEstimate(decimal Requested, decimal Filled, decimal Unfilled,
    decimal Notional, decimal? AveragePrice, decimal? ImpactPercent)
{
    public bool TradingAuthorized => false;
    public bool FullyCovered => Unfilled == 0;
}
public static class SamuelBookDepth
{
    // Static snapshot estimate only, excluding fees and subsequent market changes.
    public static SamuelDepthEstimate Evaluate(IReadOnlyList<SamuelBookLevel> levels,
        decimal quantity, bool buy)
    {
        if(quantity<=0 || levels.Count>10000 || levels.Any(x=>x.Price<=0 || x.Quantity<=0))
            throw new ArgumentException("Invalid depth inputs.");
        for(int i=1;i<levels.Count;i++)
            if(buy ? levels[i].Price<=levels[i-1].Price : levels[i].Price>=levels[i-1].Price)
                throw new ArgumentException("Invalid price ordering.");
        decimal remaining=quantity, notional=0;
        foreach(var level in levels)
        {
            var take=Math.Min(remaining,level.Quantity);
            notional+=take*level.Price;
            remaining-=take;
            if(remaining==0) break;
        }
        var filled=quantity-remaining;
        decimal? average=filled==0?null:notional/filled;
        decimal? impact=average is null?null:
            (buy?average.Value-levels[0].Price:levels[0].Price-average.Value)/levels[0].Price*100m;
        return new(quantity,filled,remaining,notional,average,impact);
    }
}
