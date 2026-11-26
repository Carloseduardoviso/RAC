namespace RAC.Application;

// Research eligibility only. Never grants execution permission.
public static class IndependentResearchGate
{
    public record Evidence(DateOnly Month, decimal Cost, int Trades, decimal Profit, decimal Loss, decimal DrawdownPct, bool Unresolved);
    // Converts a replay into gate evidence; provenance and freeze audit remain separate requirements.
    public static Evidence ReplayMonth(DateOnly month,ResearchExperimentPlan.Row row,IReadOnlyList<ResearchExperimentReplay.Minute> data,CancellationToken ct=default)
    {
        if(month.Day!=1 || month<new DateOnly(2026,12,1) || month>=new DateOnly(2027,6,1))
            throw new ArgumentException("Outside independent validation window.",nameof(month));
        _=ResearchExperimentIdentity.Create(row);
        if(data.Count!=DateTime.DaysInMonth(month.Year,month.Month)*1440)
            throw new ArgumentException("Incomplete monthly replay.",nameof(data));
        var result=ResearchExperimentReplay.RunDetailed(data,row.Parameters,ct:ct);
        return new Evidence(month,(row.Parameters.FeePctPerSide+row.Parameters.SlippagePctPerSide)/100,
            result.Summary.Trades,result.RealizedProfit,result.RealizedLoss,result.Summary.DrawdownPct,result.Summary.Unresolved);
    }
    public static string[] Evaluate(IReadOnlyList<Evidence> rows, DateTimeOffset? asOf=null)
    {
        var reasons=new List<string>();
        var months=Enumerable.Range(0,6).Select(i=>new DateOnly(2026,12,1).AddMonths(i)).ToArray();
        var costs=new[]{.0015m,.003m};
        if(rows.Count!=12 || months.Any(m=>costs.Any(c=>rows.Count(r=>r.Month==m && r.Cost==c)!=1)))
            return new[]{"INCOMPLETE_OR_DUPLICATE_EVIDENCE"};
        if(DateOnly.FromDateTime((asOf??DateTimeOffset.UtcNow).UtcDateTime)<months[^1].AddMonths(1))
            reasons.Add("VALIDATION_PERIOD_NOT_COMPLETE");
        foreach(var c in costs)
        {
            var group=rows.Where(r=>r.Cost==c).ToArray();
            if(group.Any(r=>r.Trades<0 || r.Profit<0 || r.Loss<0 || r.DrawdownPct<0 || r.DrawdownPct>100 || (r.Trades==0 && (r.Profit!=0 || r.Loss!=0))))
                reasons.Add("INVALID_METRICS");
            if(group.Sum(r=>r.Trades)<30) reasons.Add("INSUFFICIENT_TRADES");
            if(group.Count(r=>r.Profit>r.Loss)<4) reasons.Add("INSUFFICIENT_POSITIVE_MONTHS");
            var profit=group.Sum(r=>r.Profit);var loss=group.Sum(r=>r.Loss);
            if(profit<=loss || loss<=0 || profit<1.5m*loss) reasons.Add("INSUFFICIENT_PROFIT_FACTOR");
            if(group.Any(r=>r.DrawdownPct>3 || r.Unresolved)) reasons.Add("RISK_OR_UNRESOLVED_POSITION");
        }
        return reasons.Distinct().ToArray();
    }
}
