using RAC.Application;
using System.IO.Compression;
using System.Security.Cryptography;
namespace RAC.Tests;
public sealed class GabrielHistoricalArchiveReaderTests
{
    [Fact]
    public void RejectsIncompleteAndUnverifiedDiscoveryArchives()
    {
        using var stream=new MemoryStream();
        using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))
        using(var writer=new StreamWriter(zip.CreateEntry("ABCUSDT-1m-2026-05.csv").Open()))
        {
            var t=new DateTimeOffset(2026,5,1,0,0,0,TimeSpan.Zero).ToUnixTimeMilliseconds()*1000;
            writer.WriteLine($"{t},1,1,1,1,0,{t+59999999},0,0,0,0,0");
        }
        var bytes=stream.ToArray();
        var hash=Convert.ToHexString(SHA256.HashData(bytes));
        Assert.Throws<InvalidDataException>(()=>GabrielHistoricalArchiveReader.Read("ABCUSDT",2026,5,bytes,hash));
        Assert.Throws<InvalidDataException>(()=>GabrielHistoricalArchiveReader.Read("ABCUSDT",2026,5,bytes,new string('0',64)));
        Assert.Throws<ArgumentException>(()=>MonthlyArchiveResearch.Analyze("ABCUSDT",2026,5,new MemoryStream(bytes)));
    }
}
