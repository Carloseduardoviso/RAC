using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using RAC.Application;
using RAC.Domain;
internal static class ResearchExperimentCli
{
    internal static async Task<int> RunAsync()
    {
        var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RAC","research","monthly");
        var plan=ResearchExperimentPlan.Build();
        var rows=new List<object>();
        foreach(var month in new[]{"2026-05","2026-06","2026-07","2026-08","2026-09","2026-10"})
        {
            var folder=Path.Combine(root,month);
            using var doc=JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder,"manifest.json")));
            var m=doc.RootElement;
            if(m.GetProperty("month").GetString()!=month||m.GetProperty("mode").GetString()!="ISOLATED_MONTHLY_ARCHIVE_RESEARCH_ONLY"||m.GetProperty("paperModified").GetBoolean()||m.GetProperty("orderPlacementEnabled").GetBoolean()||m.GetProperty("promotionAllowed").GetBoolean())throw new InvalidDataException("Unsafe manifest");
            foreach(var symbol in RacMarketSymbols.Supported)
            {
                var name=$"{symbol}-1m-{month}.zip";
                var item=m.GetProperty("reports").EnumerateArray().Single(x=>x.GetProperty("symbol").GetString()==symbol);
                using var stream=File.OpenRead(Path.Combine(folder,name));
                if(stream.Length<=0||stream.Length>MonthlyArchiveResearch.MaximumCompressedBytes||item.GetProperty("archiveFile").GetString()!=name||!string.Equals(Convert.ToHexString(SHA256.HashData(stream)),item.GetProperty("sha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Archive mismatch");
                stream.Position=0;
                MonthlyArchiveResearch.Analyze(symbol,2026,int.Parse(month[5..],CultureInfo.InvariantCulture),stream);
                stream.Position=0;
                using var zip=new ZipArchive(stream,ZipArchiveMode.Read,true);
                using var reader=new StreamReader(zip.Entries.Single().Open());
                var data=new List<ResearchExperimentReplay.Minute>();
                while(await reader.ReadLineAsync() is {} line)
                {
                    var p=line.Split(',');
                    data.Add(new(decimal.Parse(p[1],CultureInfo.InvariantCulture),decimal.Parse(p[4],CultureInfo.InvariantCulture),decimal.Parse(p[7],CultureInfo.InvariantCulture)));
                }
                if(data.Count!=DateTime.DaysInMonth(2026,int.Parse(month[5..],CultureInfo.InvariantCulture))*1440)throw new InvalidDataException("Incomplete month");
                foreach(var row in plan.Where(x=>x.Symbol==symbol))
                    rows.Add(new {Month=month,Phase=string.CompareOrdinal(month,"2026-08")<0?"EXPLORATORY_TRAINING":"REUSED_HISTORICAL_EVALUATION",Symbol=symbol,row.Parameters,Result=ResearchExperimentReplay.Run(data,row.Parameters)});
            }
            Console.WriteLine($"MONTH={month} ROWS={rows.Count}");
        }
        if(rows.Count!=1728)throw new InvalidDataException("Incomplete experiment matrix");
        var output=Path.Combine(root,"research-experiments-mvp16314239.json");
        await File.WriteAllTextAsync(output,JsonSerializer.Serialize(new {Mode="RESEARCH_ONLY",IndependentValidation=false,TradingAuthorized=false,Count=rows.Count,Rows=rows},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"RESULT={output} COUNT={rows.Count}");
        return 0;
    }
}
