using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
namespace RAC.Application;

public static class GabrielHistoricalArchiveReader
{
    public static IReadOnlyList<ResearchExperimentReplay.Minute> Read(string symbol,int year,int month,byte[] archive,string expectedSha256)
    {
        if(archive.Length==0 || archive.Length>MonthlyArchiveResearch.MaximumCompressedBytes ||
            !string.Equals(Convert.ToHexString(SHA256.HashData(archive)),expectedSha256,StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Archive integrity mismatch.");
        using var stream=new MemoryStream(archive,writable:false);
        var report=MonthlyArchiveResearch.AnalyzeDiscovery(symbol,year,month,stream);
        stream.Position=0;
        using var zip=new ZipArchive(stream,ZipArchiveMode.Read);
        using var reader=new StreamReader(zip.Entries.Single().Open());
        var data=new List<ResearchExperimentReplay.Minute>(report.ExpectedCandles);
        while(reader.ReadLine() is {} line)
        {
            var fields=line.Split(',');
            data.Add(new(decimal.Parse(fields[1],CultureInfo.InvariantCulture),decimal.Parse(fields[4],CultureInfo.InvariantCulture),decimal.Parse(fields[7],CultureInfo.InvariantCulture)));
        }
        if(data.Count!=report.ExpectedCandles) throw new InvalidDataException("Incomplete archive.");
        return data.AsReadOnly();
    }
}
