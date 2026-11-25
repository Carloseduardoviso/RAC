using RAC.Application;
using System.Text;
using System.Security.Cryptography;
namespace RAC.Infrastructure;

public sealed class GabrielPublicArchiveClient(HttpClient http)
{
    public async Task<GabrielHistoricalMatrixBuilder.Archive> ReadAsync(string symbol,int year,int month,CancellationToken ct)
    {
        ResearchExperimentPlan.Build(new[]{symbol});
        if(year!=2026 || month is <5 or >10) throw new ArgumentOutOfRangeException(nameof(month));
        var name=$"{symbol}-1m-{year:D4}-{month:D2}.zip";
        var url=$"https://data.binance.vision/data/spot/monthly/klines/{symbol}/1m/{name}";
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        var checksum=Encoding.ASCII.GetString(await DownloadAsync(url+".CHECKSUM",1024,deadline.Token));
        var fields=checksum.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
        if(fields.Length!=2 || fields[0].Length!=64 || !fields[0].All(Uri.IsHexDigit) || fields[1]!=name)
            throw new InvalidDataException("Invalid public archive checksum.");
        var bytes=await DownloadAsync(url,MonthlyArchiveResearch.MaximumCompressedBytes,deadline.Token);
        if(!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)),fields[0],StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Public archive checksum mismatch.");
        return new(bytes,fields[0]);
    }

    private async Task<byte[]> DownloadAsync(string url,int maximum,CancellationToken ct)
    {
        using var response=await http.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,ct);
        response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength>maximum) throw new InvalidDataException("Archive exceeds size bound.");
        await using var input=await response.Content.ReadAsStreamAsync(ct);
        using var output=new MemoryStream();
        var buffer=new byte[8192];
        int read;
        while((read=await input.ReadAsync(buffer.AsMemory(),ct))!=0)
        {
            if(output.Length+read>maximum) throw new InvalidDataException("Archive exceeds size bound.");
            output.Write(buffer,0,read);
        }
        if(output.Length==0) throw new InvalidDataException("Empty archive response.");
        return output.ToArray();
    }
}
