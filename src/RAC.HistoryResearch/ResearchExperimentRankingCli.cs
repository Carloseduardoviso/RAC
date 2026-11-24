using System.Text.Json;
using RAC.Application;
using RAC.Infrastructure;
internal static class ResearchExperimentRankingCli
{
    private sealed record Row(string Month,string Phase,string Symbol,ResearchBacktestParameters.Experiment Parameters,ResearchExperimentReplay.Result Result);
    internal static async Task<int> RunAsync()
    {
        var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RAC","research","monthly");
        var path=Path.Combine(root,"research-experiments-mvp16314239.json");
        using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(path));
        var m=doc.RootElement;
        if(m.GetProperty("Mode").GetString()!="RESEARCH_ONLY"||m.GetProperty("IndependentValidation").GetBoolean()||m.GetProperty("TradingAuthorized").GetBoolean())throw new InvalidDataException("Unsafe report");
        var rows=m.GetProperty("Rows").Deserialize<Row[]>()??throw new InvalidDataException("Missing rows");
        var plan=ResearchExperimentPlan.Build();
        var months=new[]{"2026-05","2026-06","2026-07","2026-08","2026-09","2026-10"};
        if(rows.Length!=1728||rows.Select(x=>(x.Month,x.Symbol,x.Parameters)).Distinct().Count()!=1728||rows.Any(x=>!months.Contains(x.Month)||!plan.Contains(new ResearchExperimentPlan.Row(x.Symbol,x.Parameters))||x.Result.NetPct< -100||x.Result.DrawdownPct<0||x.Result.Trades<0||x.Result.Wins<0||x.Result.Wins>x.Result.Trades||x.Result.Costs<0))throw new InvalidDataException("Invalid matrix");
        var ranking=rows.GroupBy(x=>(x.Symbol,Base:x.Parameters with {SlippagePctPerSide=0})).Select(g=>{
            var normal=g.Where(x=>x.Parameters.SlippagePctPerSide==.05m).ToArray();
            var stress=g.Where(x=>x.Parameters.SlippagePctPerSide==.20m).ToArray();
            if(normal.Length!=6||stress.Length!=6)throw new InvalidDataException("Incomplete cost scenarios");
            var evaluation=stress.Where(x=>string.CompareOrdinal(x.Month,"2026-08")>=0).ToArray();
            var trades=stress.Sum(x=>x.Result.Trades);
            var wins=stress.Sum(x=>x.Result.Wins);
            var positive=stress.Count(x=>x.Result.NetPct>0);
            var unresolved=g.Count(x=>x.Result.Unresolved);
            return new {g.Key.Symbol,Parameters=g.Key.Base,NormalMeanPct=normal.Average(x=>x.Result.NetPct),StressMeanPct=stress.Average(x=>x.Result.NetPct),StressEvaluationMeanPct=evaluation.Average(x=>x.Result.NetPct),PositiveStressMonths=positive,WorstStressMonthPct=stress.Min(x=>x.Result.NetPct),MaxDrawdownPct=g.Max(x=>x.Result.DrawdownPct),Trades=trades,WinRatePct=trades>0?(decimal?)wins*100/trades:null,StressCosts=stress.Sum(x=>x.Result.Costs),MonthlyStressProfitFactors=stress.OrderBy(x=>x.Month).Select(x=>x.Result.ProfitFactor).ToArray(),Unresolved=unresolved,Status="EXPLORATORY_ONLY",TradingAuthorized=false};
        }).OrderByDescending(x=>x.Unresolved==0&&x.Trades>=30&&x.PositiveStressMonths>=4&&x.StressEvaluationMeanPct>0).ThenByDescending(x=>x.StressEvaluationMeanPct).ThenBy(x=>x.MaxDrawdownPct).ThenBy(x=>x.Symbol,StringComparer.Ordinal).ToArray();
        var report=new {Mode="RESEARCH_ONLY",IndependentValidation=false,TradingAuthorized=false,Count=ranking.Length,Rows=ranking};
        await File.WriteAllTextAsync(Path.Combine(root,"research-ranking-mvp16314240.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"RANKED={ranking.Length} POSITIVE_STRESS={ranking.Count(x=>x.StressMeanPct>0)} SCREEN_PASS={ranking.Count(x=>x.Unresolved==0&&x.Trades>=30&&x.PositiveStressMonths>=4&&x.StressEvaluationMeanPct>0)}");
        Console.WriteLine(JsonSerializer.Serialize(ranking.Take(5)));
        return 0;
    }
}
