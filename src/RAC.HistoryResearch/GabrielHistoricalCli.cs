using System.Text.Json;
using System.Security.Cryptography;
using RAC.Application;

internal static class GabrielHistoricalCli
{
    internal static async Task<int> RunAsync()
    {
        var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RAC","research","monthly");
        var path=Path.Combine(root,"research-experiments-mvp16314239.json");
        if(new FileInfo(path).Length>8*1024*1024) throw new InvalidDataException("Matrix too large.");
        var bytes=await File.ReadAllBytesAsync(path);
        var rows=GabrielExperimentMatrix.Read(System.Text.Encoding.UTF8.GetString(bytes));
        var report=new {Mode="RESEARCH_ONLY",TradingAuthorized=false,IndependentValidation=false,
            SourceSha256=Convert.ToHexString(SHA256.HashData(bytes)),Count=rows.Count,
            PositiveMean=rows.Count(x=>x.Evidence.MeanNetPercent>0),
            Unresolved=rows.Count(x=>x.Evidence.UnresolvedMonths>0),Rows=rows};
        var json=JsonSerializer.Serialize(report,new JsonSerializerOptions{WriteIndented=true});
        var output=Path.Combine(root,"gabriel-historical-v1-"+report.SourceSha256+".json");
        if(File.Exists(output))
        {
            if(await File.ReadAllTextAsync(output)!=json) throw new InvalidDataException("Conflicting existing report.");
        }
        else
        {
            using var stream=new FileStream(output,FileMode.CreateNew);
            await using var writer=new StreamWriter(stream);
            await writer.WriteAsync(json);
        }
        Console.WriteLine($"GABRIEL_CONFIGURATIONS={report.Count} POSITIVE_MEAN={report.PositiveMean} UNRESOLVED={report.Unresolved}");
        return 0;
    }
}
