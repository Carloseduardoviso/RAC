namespace RAC.Application;

public static class GabrielStrategyRanking
{
    public sealed record Ranked(int Rank,GabrielExperimentMatrix.Assessment Assessment,string Status)
    {
        public bool TradingAuthorized => false;
    }
    public static IReadOnlyList<ResearchExperimentPlan.Row> FreezeCandidates(IReadOnlyList<GabrielExperimentMatrix.Assessment> rows)
        => Rank(rows).Where(x=>x.Status=="INDEPENDENT_VALIDATION_REQUIRED" && x.Assessment.Evidence.Months==6)
            .Select(x=>new ResearchExperimentPlan.Row(x.Assessment.Symbol,x.Assessment.Parameters)).Distinct().ToArray();
    // Stress evidence selects candidates; normal-cost evidence is a required companion.
    public static IReadOnlyList<ResearchExperimentPlan.Row> FreezePairs(IReadOnlyList<GabrielExperimentMatrix.Assessment> rows)
    {
        var result=new List<ResearchExperimentPlan.Row>();
        foreach(var stress in FreezeCandidates(rows))
        {
            var normal=stress with {Parameters=stress.Parameters with {SlippagePctPerSide=.05m}};
            if(rows.Count(x=>x.Symbol==normal.Symbol && x.Parameters==normal.Parameters && x.Evidence.Months==6)!=1)
                throw new InvalidOperationException("Missing or ambiguous normal-cost companion.");
            result.Add(stress);
            result.Add(normal);
        }
        return result.Distinct().ToArray();
    }
    public static IReadOnlyList<Ranked> Rank(IReadOnlyList<GabrielExperimentMatrix.Assessment> rows)
    {
        return rows.Where(x=>x.Parameters.SlippagePctPerSide==.20m)
            .OrderByDescending(x=>x.Evidence.UnresolvedMonths==0 && x.Evidence.MeanNetPercent>0 && x.Evidence.Trades>=30)
            .ThenByDescending(x=>x.Evidence.MeanNetPercent)
            .ThenBy(x=>x.Evidence.WorstMonthlyDrawdownPercent)
            .ThenBy(x=>x.Evidence.MonthlyVolatilityPercent ?? double.MaxValue)
            .ThenBy(x=>x.Symbol,StringComparer.Ordinal)
            .ThenBy(x=>x.Parameters.TemplateId,StringComparer.Ordinal)
            .ThenBy(x=>x.Parameters.CandleMinutes)
            .ThenBy(x=>x.Parameters.FastPeriod)
            .ThenBy(x=>x.Parameters.SlowPeriod)
            .Select((x,i)=>new Ranked(i+1,x,x.Evidence.UnresolvedMonths>0?"UNRESOLVED_POSITIONS":
                x.Evidence.MeanNetPercent<=0?"NON_POSITIVE_NET_RETURN":
                x.Evidence.Trades<30?"INSUFFICIENT_TRADES":"INDEPENDENT_VALIDATION_REQUIRED")).ToArray();
    }
}
