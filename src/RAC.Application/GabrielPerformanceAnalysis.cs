namespace RAC.Application;

public sealed record GabrielPerformanceMetrics(int Observations,decimal? MeanPercent,
    double? SampleVolatilityPercent,decimal? ProfitFactor,double? CompoundedReturnPercent,
    double? MaxDrawdownPercent)
{
    public bool TradingAuthorized => false;
}

public static class GabrielPerformanceAnalysis
{
    // Ordered net portfolio-period returns, not overlapping trade returns.
    public static GabrielPerformanceMetrics Calculate(IReadOnlyList<decimal> returns)
    {
        if(returns.Count>100000 || returns.Any(x=>x < -100m || x > 10000m))
            throw new ArgumentException("Invalid return series.");
        if(returns.Count==0) return new(0,null,null,null,null,null);
        var mean=returns.Average();
        double? volatility=returns.Count>1
            ? Math.Sqrt(returns.Sum(x=>Math.Pow((double)(x-mean),2))/(returns.Count-1)) : null;
        var gains=returns.Where(x=>x>0).Sum();
        var losses=-returns.Where(x=>x<0).Sum();
        double equity=1,peak=1,drawdown=0;
        foreach(var value in returns)
        {
            equity*=1+(double)value/100;
            if(!double.IsFinite(equity)) throw new ArgumentException("Return series overflow.");
            peak=Math.Max(peak,equity);
            drawdown=Math.Max(drawdown,(1-equity/peak)*100);
        }
        var compounded=(equity-1)*100;
        if(!double.IsFinite(compounded)) throw new ArgumentException("Return series overflow.");
        return new(returns.Count,mean,volatility,losses>0?gains/losses:null,compounded,drawdown);
    }
}
