using System.Text.Json;
using RAC.Application;
using RAC.Infrastructure;
internal static class ResearchAgentFilterSelectionCli
{
    private sealed record Row(string Month,string Symbol,ResearchBacktestParameters.Experiment Parameters,ResearchExperimentReplay.Result Result);
    internal static async Task<int> RunAsync()
    {
        var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RAC","research","monthly");
        var files=new Dictionary<string,string>{{"Baseline","research-experiments-mvp16314239.json"},{"All","research-experiments-mvp16314244-filtered.json"},{"Volume","research-filter-Volume.json"},{"Volatility","research-filter-Volatility.json"},{"Trend","research-filter-Trend.json"},{"Cost","research-filter-Cost.json"}};
        var matrix=new List<(string Mode,Row Row)>();
        var plan=ResearchExperimentPlan.Build();
        var months=new[]{"2026-05","2026-06","2026-07","2026-08","2026-09","2026-10"};
        foreach(var file in files)
        {
            using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,file.Value)));
            var m=doc.RootElement;
            if(m.GetProperty("Mode").GetString()!="RESEARCH_ONLY"||m.GetProperty("TradingAuthorized").GetBoolean()||m.GetProperty("IndependentValidation").GetBoolean())throw new InvalidDataException("Unsafe input");
            var rows=m.GetProperty("Rows").Deserialize<Row[]>()??throw new InvalidDataException("Missing rows");
            if(rows.Length!=1728||rows.Select(x=>(x.Month,x.Symbol,x.Parameters)).Distinct().Count()!=1728||rows.Any(x=>!months.Contains(x.Month)||!plan.Contains(new ResearchExperimentPlan.Row(x.Symbol,x.Parameters))))throw new InvalidDataException("Invalid matrix");
            matrix.AddRange(rows.Select(x=>(file.Key,x)));
        }
        var candidates=matrix.Where(x=>x.Row.Parameters.SlippagePctPerSide==.20m).GroupBy(x=>(x.Mode,x.Row.Symbol,x.Row.Parameters)).Select(g=>{
            var r=g.Select(x=>x.Row).ToArray();
            if(r.Length!=6)throw new InvalidDataException("Incomplete candidate");
            var train=r.Where(x=>string.CompareOrdinal(x.Month,"2026-08")<0).ToArray();
            var eval=r.Except(train).ToArray();
            var decision=ResearchLeadershipReview.Evaluate(new(r.Average(x=>x.Result.NetPct),eval.Average(x=>x.Result.NetPct),r.Count(x=>x.Result.NetPct>0),r.Sum(x=>x.Result.Trades),r.Max(x=>x.Result.DrawdownPct),r.Count(x=>x.Result.Unresolved)));
            return new {g.Key.Mode,g.Key.Symbol,g.Key.Parameters,TrainingMean=train.Average(x=>x.Result.NetPct),EvaluationMean=eval.Average(x=>x.Result.NetPct),FullMean=r.Average(x=>x.Result.NetPct),Trades=r.Sum(x=>x.Result.Trades),Decision=decision};
        }).ToArray();
        var selected=candidates.GroupBy(x=>x.Symbol).Select(g=>g.OrderByDescending(x=>x.TrainingMean).ThenBy(x=>x.Mode,StringComparer.Ordinal).ThenBy(x=>x.Parameters.TemplateId,StringComparer.Ordinal).ThenBy(x=>x.Parameters.CandleMinutes).ThenBy(x=>x.Parameters.SlowPeriod).First()).ToArray();
        await File.WriteAllTextAsync(Path.Combine(root,"research-agent-selection-mvp16314247.json"),JsonSerializer.Serialize(new {Mode="RESEARCH_ONLY",IndependentValidation=false,TradingAuthorized=false,Candidates=candidates,Selected=selected},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"CANDIDATES={candidates.Length} SELECTED={selected.Length} SCREEN_PASS={selected.Count(x=>x.Decision.Ana=="QUEUE_INDEPENDENT_RESEARCH")}");
        Console.WriteLine(JsonSerializer.Serialize(selected.Select(x=>new{x.Symbol,x.Mode,x.TrainingMean,x.EvaluationMean,x.FullMean,x.Trades,x.Decision.Ana})));
        return 0;
    }
}
