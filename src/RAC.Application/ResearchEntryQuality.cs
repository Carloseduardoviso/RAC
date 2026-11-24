namespace RAC.Application;
public static class ResearchEntryQuality
{
    public sealed record Decision(bool Allowed,string Reason);
    public static Decision Evaluate(IReadOnlyList<ResearchExperimentReplay.Minute> data,int index,int interval,decimal budget,decimal targetPct,decimal costPct)
    {
        if(interval is not (60 or 120)||index<3*interval||index>data.Count||budget<=0||targetPct<=0||costPct<0)return new(false,"INVALID_OR_INSUFFICIENT_DATA");
        var recent=data.Skip(index-interval).Take(interval).ToArray();
        var prior=data.Skip(index-3*interval).Take(2*interval).ToArray();
        if(recent.Concat(prior).Any(x=>x.Close<=0||x.QuoteVolume<0))return new(false,"INVALID_DATA");
        if(budget>recent[^1].QuoteVolume*.001m)return new(false,"LIQUIDITY");
        if(recent.Sum(x=>x.QuoteVolume)<prior.Sum(x=>x.QuoteVolume)/2)return new(false,"LOW_VOLUME");
        if(recent.Max(x=>x.Close)/recent.Min(x=>x.Close)>1.03m)return new(false,"VOLATILITY");
        if(recent[^1].Close<prior.Average(x=>x.Close))return new(false,"DOWNTREND");
        if(targetPct<4*costPct)return new(false,"COST_MARGIN");
        return new(true,"RESEARCH_FILTER_PASS");
    }
}
