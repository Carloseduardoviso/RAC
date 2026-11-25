namespace RAC.Application;

public sealed record GabrielMonthlyReplay(string Month,ResearchExperimentReplay.Result Result);
public sealed record GabrielHistoricalAssessment(int Months,decimal MeanNetPercent,double? MonthlyVolatilityPercent,
    decimal WorstMonthlyDrawdownPercent,int Trades,int Wins,int UnresolvedMonths,string Status)
{
    public bool TradingAuthorized => false;
}

public static class GabrielHistoricalAnalysis
{
    public static GabrielHistoricalAssessment Evaluate(IReadOnlyList<GabrielMonthlyReplay> rows)
    {
        if(rows.Count==0 || rows.Select(x=>x.Month).Distinct().Count()!=rows.Count ||
            rows.Any(x=>!DateTime.TryParseExact(x.Month,"yyyy-MM",System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,out _) || x.Result.DrawdownPct is <0 or >100 ||
                x.Result.Trades<0 || x.Result.Wins<0 || x.Result.Wins>x.Result.Trades || x.Result.Costs<0))
            throw new ArgumentException("Invalid monthly evidence.");
        var metrics=GabrielPerformanceAnalysis.Calculate(rows.OrderBy(x=>x.Month,StringComparer.Ordinal).Select(x=>x.Result.NetPct).ToArray());
        var unresolved=rows.Count(x=>x.Result.Unresolved);
        return new(rows.Count,metrics.MeanPercent!.Value,metrics.SampleVolatilityPercent,
            rows.Max(x=>x.Result.DrawdownPct),rows.Sum(x=>x.Result.Trades),rows.Sum(x=>x.Result.Wins),unresolved,
            unresolved>0?"UNRESOLVED_POSITIONS": "EXPLORATORY_ONLY");
    }
}
