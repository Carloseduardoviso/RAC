using System.Text.Json;
using System.Security.Cryptography;
using RAC.Application;
using RAC.Infrastructure;
internal static class ResearchStrategyCatalogCli
{
    internal static async Task<int> RunAsync()
    {
        var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RAC","research","monthly");
        var bytes=await File.ReadAllBytesAsync(Path.Combine(root,"research-agent-selection-mvp16314247.json"));
        using var doc=JsonDocument.Parse(bytes);
        var m=doc.RootElement;
        if(m.GetProperty("Mode").GetString()!="RESEARCH_ONLY"||m.GetProperty("TradingAuthorized").GetBoolean()||m.GetProperty("IndependentValidation").GetBoolean())throw new InvalidDataException("Unsafe source");
        var selected=m.GetProperty("Selected").EnumerateArray().ToArray();
        var plan=ResearchExperimentPlan.Build();
        if(selected.Length!=12||selected.Select(x=>x.GetProperty("Symbol").GetString()).Distinct().Count()!=12)throw new InvalidDataException("Invalid selection");
        foreach(var x in selected)
        {
            var p=x.GetProperty("Parameters").Deserialize<ResearchBacktestParameters.Experiment>()??throw new InvalidDataException("Missing parameters");
            if(!plan.Contains(new ResearchExperimentPlan.Row(x.GetProperty("Symbol").GetString()!,p))||p.SlippagePctPerSide!=.20m||x.GetProperty("Decision").GetProperty("TradingAuthorized").GetBoolean())throw new InvalidDataException("Invalid candidate");
        }
        var hash=Convert.ToHexString(SHA256.HashData(bytes));
        var catalog=new {SchemaVersion=1,Version="MVP1.6.3.14.2.48",SourceSha256=hash,Mode="RESEARCH_ONLY",IndependentValidation=false,TradingAuthorized=false,TrainingMonths="2026-05..2026-07",ReusedEvaluationMonths="2026-08..2026-10",Entries=selected.Select(x=>new {Status="ARCHIVED_EXPLORATORY",TradingAuthorized=false,Evidence=x}).ToArray()};
        var output=Path.Combine(root,$"research-catalog-v1-{hash}.json");
        var content=JsonSerializer.SerializeToUtf8Bytes(catalog,new JsonSerializerOptions{WriteIndented=true});
        if(File.Exists(output))
        {
            if(!(await File.ReadAllBytesAsync(output)).SequenceEqual(content))throw new InvalidDataException("Catalog conflict");
        }
        else
        {
            await using var stream=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.None);
            await stream.WriteAsync(content);
        }
        Console.WriteLine($"CATALOG={output} ENTRIES={selected.Length} TRADING_AUTHORIZED=false");
        return 0;
    }
}
