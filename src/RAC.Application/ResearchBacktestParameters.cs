namespace RAC.Infrastructure;
public static class ResearchBacktestParameters
{
    public sealed record Experiment(string TemplateId,int CandleMinutes,int FastPeriod,int SlowPeriod,decimal ThresholdPct,decimal StopPct,decimal TargetPct,decimal FeePctPerSide,decimal SlippagePctPerSide,decimal ExposurePct,string Status,bool TradingAuthorized);
    public static IReadOnlyList<Experiment> Generate(ResearchStrategyCandidates.Candidate candidate)
    {
        if(candidate.TradingAuthorized||candidate.Status!="HYPOTHESIS_UNVALIDATED")return [];
        var configurations=candidate.TemplateId switch
        {
            "MA_CROSS_V1"=>new (int Fast,int Slow,decimal Threshold)[]{(12,26,0),(20,50,0)},
            "MEAN_REVERSION_V1"=>new (int Fast,int Slow,decimal Threshold)[]{(0,20,1),(0,50,2)},
            "MOMENTUM_V1"=>new (int Fast,int Slow,decimal Threshold)[]{(0,12,1),(0,24,2)},
            _=>Array.Empty<(int Fast,int Slow,decimal Threshold)>()
        };
        return (from c in configurations
                from minutes in new[]{60,120}
                from slippage in new[]{0.05m,0.20m}
                select new Experiment(candidate.TemplateId,minutes,c.Fast,c.Slow,c.Threshold,2m,4m,0.10m,slippage,10m,"RESEARCH_ONLY",false)).ToArray();
    }
}
