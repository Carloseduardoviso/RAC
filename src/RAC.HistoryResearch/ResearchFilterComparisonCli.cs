using System.Text.Json;
using RAC.Application;
using RAC.Infrastructure;
internal static class ResearchFilterComparisonCli
{
    private sealed record Row(string Month,string Symbol,ResearchBacktestParameters.Experiment Parameters,ResearchExperimentReplay.Result Baseline,ResearchExperimentReplay.Result Filtered,decimal DeltaNetPct);
    internal static async Task<int> RunAsync()
    {
        var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RAC","research","monthly");
        using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,"research-comparison-mvp16314244.json")));
        var summary=doc.RootElement.GetProperty("Summary");
        if(summary.GetProperty("Mode").GetString()!="RESEARCH_ONLY"||summary.GetProperty("TradingAuthorized").GetBoolean()||summary.GetProperty("IndependentValidation").GetBoolean())throw new InvalidDataException("Unsafe comparison");
        var rows=doc.RootElement.GetProperty("Rows").Deserialize<Row[]>()??throw new InvalidDataException("Missing rows");
        if(rows.Length!=1728||rows.Select(x=>(x.Month,x.Symbol,x.Parameters)).Distinct().Count()!=1728)throw new InvalidDataException("Incomplete matrix");
        var groups=rows.GroupBy(x=>x.Symbol).Select(g=>{
            if(g.Count()!=144)throw new InvalidDataException("Incomplete symbol");
            var b=g.Select(x=>x.Baseline).ToArray();var f=g.Select(x=>x.Filtered).ToArray();
            var bt=b.Sum(x=>x.Trades);var ft=f.Sum(x=>x.Trades);
            return new {Symbol=g.Key,BaselineMean=b.Average(x=>x.NetPct),FilteredMean=f.Average(x=>x.NetPct),Delta=f.Average(x=>x.NetPct)-b.Average(x=>x.NetPct),BaselineMaxDrawdown=b.Max(x=>x.DrawdownPct),FilteredMaxDrawdown=f.Max(x=>x.DrawdownPct),BaselineCosts=b.Sum(x=>x.Costs),FilteredCosts=f.Sum(x=>x.Costs),BaselineTrades=bt,FilteredTrades=ft,BaselineWinRate=bt>0?(decimal?)b.Sum(x=>x.Wins)*100/bt:null,FilteredWinRate=ft>0?(decimal?)f.Sum(x=>x.Wins)*100/ft:null,BaselineUnresolved=b.Count(x=>x.Unresolved),FilteredUnresolved=f.Count(x=>x.Unresolved)};
        }).OrderByDescending(x=>x.FilteredMean).ToArray();
        if(groups.Length!=12)throw new InvalidDataException("Incomplete agents");
        await File.WriteAllTextAsync(Path.Combine(root,"research-filter-agents-mvp16314245.json"),JsonSerializer.Serialize(new {Mode="RESEARCH_ONLY",TradingAuthorized=false,IndependentValidation=false,Rows=groups},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine(JsonSerializer.Serialize(groups));
        return 0;
    }
}
