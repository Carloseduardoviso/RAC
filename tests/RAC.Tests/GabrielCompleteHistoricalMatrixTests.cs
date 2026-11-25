using RAC.Application;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace RAC.Tests;

public sealed class GabrielCompleteHistoricalMatrixTests
{
    [Fact]
    public async Task BuildsAllExperimentsFromSixCompleteSyntheticMonths()
    {
        var months=new List<int>();
        var json=await GabrielHistoricalMatrixBuilder.BuildAsync(new[]{"ABCUSDT"},
            (symbol,year,month,ct)=>{
                ct.ThrowIfCancellationRequested();
                Assert.Equal("ABCUSDT",symbol);
                Assert.Equal(2026,year);
                months.Add(month);
                using var stream=new MemoryStream();
                using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))
                using(var writer=new StreamWriter(zip.CreateEntry($"{symbol}-1m-{year:D4}-{month:D2}.csv").Open()))
                {
                    var first=new DateTimeOffset(year,month,1,0,0,0,TimeSpan.Zero).ToUnixTimeMilliseconds()*1000;
                    for(var i=0;i<DateTime.DaysInMonth(year,month)*1440;i++)
                    {
                        var t=first+i*60000000L;
                        writer.WriteLine($"{t},100,100,100,100,10,{t+59999999},1000,1,0,0,0");
                    }
                }
                var bytes=stream.ToArray();
                return Task.FromResult(new GabrielHistoricalMatrixBuilder.Archive(bytes,Convert.ToHexString(SHA256.HashData(bytes))));
            },default);
        Assert.Equal(new[]{5,6,7,8,9,10},months);
        using var doc=JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("TradingAuthorized").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("IndependentValidation").GetBoolean());
        Assert.Equal(144,doc.RootElement.GetProperty("Rows").GetArrayLength());
        var matrix=GabrielExperimentMatrix.Read(json,new[]{"ABCUSDT"});
        Assert.Equal(24,matrix.Count);
        Assert.All(matrix,row=>Assert.Equal("ABCUSDT",row.Symbol));
    }
}
