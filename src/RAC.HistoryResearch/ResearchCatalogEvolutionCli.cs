using System.Text.Json;
using System.Security.Cryptography;
internal static class ResearchCatalogEvolutionCli
{
    internal static async Task<int> RunAsync(string[] args)
    {
        var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RAC","research","monthly");
        if(args.Length!=3)throw new ArgumentException("Specify two catalog SHA256 identifiers");
        async Task<JsonElement> Load(string hash)
        {
            if(hash.Length!=64||!hash.All(Uri.IsHexDigit))throw new ArgumentException("Invalid identifier");
            using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(root,$"research-catalog-v1-{hash.ToUpperInvariant()}.json")));
            var m=doc.RootElement;
            if(m.GetProperty("SchemaVersion").GetInt32()!=1||m.GetProperty("Mode").GetString()!="RESEARCH_ONLY"||m.GetProperty("TradingAuthorized").GetBoolean()||m.GetProperty("IndependentValidation").GetBoolean()||!string.Equals(m.GetProperty("SourceSha256").GetString(),hash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Invalid catalog");
            return m.Clone();
        }
        var before=await Load(args[1]);var after=await Load(args[2]);
        foreach(var field in new[]{"TrainingMonths","ReusedEvaluationMonths"})
            if(before.GetProperty(field).GetString()!=after.GetProperty(field).GetString())throw new InvalidDataException("Incomparable periods");
        Dictionary<string,JsonElement> Entries(JsonElement m)
        {
            var entries=m.GetProperty("Entries").EnumerateArray().Select(x=>{
                if(x.GetProperty("TradingAuthorized").GetBoolean())throw new InvalidDataException("Unsafe entry");
                return x.GetProperty("Evidence");
            }).ToDictionary(x=>x.GetProperty("Symbol").GetString()!,StringComparer.Ordinal);
            if(entries.Count!=12)throw new InvalidDataException("Incomplete catalog");
            return entries;
        }
        var a=Entries(before);var b=Entries(after);
        var rows=a.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>{
            var old=x.Value;var next=b[x.Key];
            var delta=next.GetProperty("FullMean").GetDecimal()-old.GetProperty("FullMean").GetDecimal();
            var evaluation=next.GetProperty("EvaluationMean").GetDecimal()-old.GetProperty("EvaluationMean").GetDecimal();
            return new {Symbol=x.Key,FullDeltaPct=delta,EvaluationDeltaPct=evaluation,TradesDelta=next.GetProperty("Trades").GetInt32()-old.GetProperty("Trades").GetInt32(),Classification=delta>0&&evaluation>0?"IMPROVED_HISTORICAL":delta<0||evaluation<0?"REGRESSION_OR_MIXED":delta==0&&evaluation==0?"UNCHANGED":"MIXED",TradingAuthorized=false};
        }).ToArray();
        var report=new {Mode="RESEARCH_ONLY",IndependentValidation=false,TradingAuthorized=false,Before=args[1],After=args[2],SameSource=string.Equals(args[1],args[2],StringComparison.OrdinalIgnoreCase),Rows=rows};
        var content=JsonSerializer.SerializeToUtf8Bytes(report,new JsonSerializerOptions{WriteIndented=true});
        var output=Path.Combine(root,$"research-evolution-{Convert.ToHexString(SHA256.HashData(content))}.json");
        if(!File.Exists(output)){await using var stream=new FileStream(output,FileMode.CreateNew);await stream.WriteAsync(content);}
        else if(!(await File.ReadAllBytesAsync(output)).SequenceEqual(content))throw new InvalidDataException("Report conflict");
        Console.WriteLine(JsonSerializer.Serialize(report));
        return 0;
    }
}
