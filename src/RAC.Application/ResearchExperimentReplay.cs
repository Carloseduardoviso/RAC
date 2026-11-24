using RAC.Infrastructure;
namespace RAC.Application;
public static class ResearchExperimentReplay
{
    public sealed record Minute(decimal Open,decimal Close,decimal QuoteVolume);
    public sealed record Result(decimal NetPct,decimal DrawdownPct,int Trades,int Wins,decimal? ProfitFactor,decimal Costs,bool Unresolved);
    public static Result Run(IReadOnlyList<Minute> data,ResearchBacktestParameters.Experiment p,bool qualityFilters=false,ResearchEntryFilter filters=ResearchEntryFilter.All)
    {
        if(p.TradingAuthorized||p.Status!="RESEARCH_ONLY"||p.CandleMinutes is not (60 or 120)||p.SlowPeriod<2||p.FastPeriod<0||p.FastPeriod>=p.SlowPeriod||p.ExposurePct<=0||p.ExposurePct>100||p.StopPct<=0||p.TargetPct<=0||p.FeePctPerSide<0||p.SlippagePctPerSide<0||p.FeePctPerSide+p.SlippagePctPerSide>=100||data.Any(x=>x.Open<=0||x.Close<=0||x.QuoteVolume<0))throw new ArgumentException("Invalid experiment");
        var bars=new List<decimal>();
        decimal cash=1000,qty=0,entry=0,budget=0,peak=1000,dd=0,profit=0,loss=0,costs=0;
        decimal cost=(p.FeePctPerSide+p.SlippagePctPerSide)/100;
        int trades=0,wins=0;
        bool pending=false;
        for(int i=1;i<data.Count;i++)
        {
            var price=data[i].Open;
            bool buy=false,sell=false;
            if(i%p.CandleMinutes==0)
            {
                bars.Add(data[i-1].Close);
                if(bars.Count>p.SlowPeriod)
                {
                    var close=bars[^1];var slow=bars.TakeLast(p.SlowPeriod).Average();
                    switch(p.TemplateId)
                    {
                        case "MA_CROSS_V1":
                            if(p.FastPeriod==0)throw new ArgumentException("Fast period required");
                            var fast=bars.TakeLast(p.FastPeriod).Average();
                            var previous=bars.SkipLast(1);
                            buy=fast>slow&&previous.TakeLast(p.FastPeriod).Average()<=previous.TakeLast(p.SlowPeriod).Average();sell=fast<slow;break;
                        case "MEAN_REVERSION_V1":buy=close<slow*(1-p.ThresholdPct/100);sell=close>=slow;break;
                        case "MOMENTUM_V1":var change=(close/bars[^(p.SlowPeriod+1)]-1)*100;buy=change>=p.ThresholdPct;sell=change<=0;break;
                        default:throw new ArgumentException("Unknown template");
                    }
                }
            }
            var capacity=data[i-1].QuoteVolume*.001m;
            if(qty>0)
            {
                pending=pending||sell||price<=entry*(1-p.StopPct/100)||price>=entry*(1+p.TargetPct/100);
                if(pending&&qty*price<=capacity)
                {
                    var gross=qty*price;var proceeds=gross*(1-cost);var pnl=proceeds-budget;
                    costs+=gross*cost;cash+=proceeds;qty=0;pending=false;trades++;
                    if(pnl>0){wins++;profit+=pnl;}else loss-=pnl;
                }
            }
            else if(buy)
            {
                budget=cash*p.ExposurePct/100;
                if(budget>0&&budget<=capacity&&(!qualityFilters||ResearchEntryQuality.Evaluate(data,i,p.CandleMinutes,budget,p.TargetPct,p.FeePctPerSide+p.SlippagePctPerSide,filters).Allowed)){qty=budget*(1-cost)/price;cash-=budget;entry=price;costs+=budget*cost;}
            }
            var equity=cash+qty*price*(1-cost);peak=Math.Max(peak,equity);dd=Math.Max(dd,1-equity/peak);
        }
        var final=cash+(data.Count==0?0:qty*data[^1].Close*(1-cost));dd=Math.Max(dd,1-final/peak);
        return new((final/1000-1)*100,dd*100,trades,wins,loss>0?profit/loss:null,costs,qty>0);
    }
}
