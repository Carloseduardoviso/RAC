using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using RAC.Application;
using RAC.Domain;

// Manual invocation only. No API registration, worker, trading state or secrets.
// Example: dotnet run --project src/RAC.HistoryResearch -- 2026-10
if (args.Length != 1 || !DateOnly.TryParseExact(args[0] + "-01", "yyyy-MM-dd",
        CultureInfo.InvariantCulture, DateTimeStyles.None, out var selected))
{
    Console.Error.WriteLine("Usage: RAC.HistoryResearch YYYY-MM (last fully completed UTC month or earlier)");
    return 2;
}
var utcMonth = new DateTimeOffset(DateTimeOffset.UtcNow.Year,
    DateTimeOffset.UtcNow.Month, 1, 0, 0, 0, TimeSpan.Zero);
var selectedMonth = new DateTimeOffset(selected.Year, selected.Month, 1, 0, 0, 0,
    TimeSpan.Zero);
if (selectedMonth < new DateTimeOffset(2017, 8, 1, 0, 0, 0, TimeSpan.Zero) ||
    selectedMonth >= utcMonth)
{
    Console.Error.WriteLine("Only completed UTC months between August 2017 and last month are permitted.");
    return 2;
}
var month = selected.ToString("yyyy-MM", CultureInfo.InvariantCulture);
var destination = Path.Combine(Environment.GetFolderPath(
    Environment.SpecialFolder.LocalApplicationData), "RAC", "research", "monthly", month);
Directory.CreateDirectory(destination);
using var http = new HttpClient {
    BaseAddress = new Uri("https://data.binance.vision/"),
    Timeout = TimeSpan.FromSeconds(45)
};
http.DefaultRequestHeaders.UserAgent.ParseAdd("RAC-OfflineHistoryResearch/1.3.5");
var reports = new List<object>();
foreach (var symbol in RacMarketSymbols.Supported)
{
    var name = $"{symbol}-1m-{month}.zip";
    var path = Path.Combine(destination, name);
    var url = $"data/spot/monthly/klines/{symbol}/1m/{name}";
    byte[] bytes;
    if (File.Exists(path))
    {
        var info = new FileInfo(path);
        if (info.Length <= 0 || info.Length > MonthlyArchiveResearch.MaximumCompressedBytes)
            throw new InvalidDataException("Invalid cached research archive size.");
        bytes = await File.ReadAllBytesAsync(path);
    }
    else
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (response.StatusCode == HttpStatusCode.TooManyRequests ||
            (int)response.StatusCode == 418)
            throw new HttpRequestException("Rate-limited: aborting all further downloads.");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > MonthlyArchiveResearch.MaximumCompressedBytes)
            throw new InvalidDataException("Archive response exceeds download limit.");
        await using var input = await response.Content.ReadAsStreamAsync();
        await using var buffer = new MemoryStream();
        var segment = new byte[64 * 1024];
        int read;
        while ((read = await input.ReadAsync(segment)) > 0)
        {
            if (buffer.Length + read > MonthlyArchiveResearch.MaximumCompressedBytes)
                throw new InvalidDataException("Archive exceeded maximum size during download.");
            buffer.Write(segment, 0, read);
        }
        bytes = buffer.ToArray();
    }
    using var archive = new MemoryStream(bytes, writable: false);
    var report = MonthlyArchiveResearch.Analyze(symbol, selected.Year,
        selected.Month, archive);
    var sha = Convert.ToHexString(SHA256.HashData(bytes));
    if (!File.Exists(path))
    {
        var temp = Path.Combine(destination, name + ".partial");
        try
        {
            await File.WriteAllBytesAsync(temp, bytes);
            File.Move(temp, path, overwrite: false);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    reports.Add(new {
        symbol, archiveFile = name, sha256 = sha,
        report.ExpectedCandles, report.ValidatedCandles, report.CalendarDays,
        report.UpDays, report.DownDays, report.SidewaysDays,
        report.FirstOpenUtc, report.LastOpenUtc,
        report.PaperModified, report.OrderPlacementEnabled,
        report.PromotionAllowed, report.Days
    });
    Console.WriteLine($"VALIDATED {symbol} {report.ValidatedCandles} candles, days={report.CalendarDays}, sha256={sha[..12]}...");
}
var manifest = new {
    mode = "ISOLATED_MONTHLY_ARCHIVE_RESEARCH_ONLY",
    source = "https://data.binance.vision/data/spot/monthly/klines/",
    month, observedUtc = DateTimeOffset.UtcNow,
    symbols = reports.Count, paperModified = false,
    historyPersistedInOperationalDatabase = false,
    orderPlacementEnabled = false, promotionAllowed = false,
    warning = "Public archived 1-minute candles, descriptive daily regimes only. No trading strategy validated.",
    reports
};
var manifestFile = Path.Combine(destination, "manifest.json");
var tmpManifest = manifestFile + ".partial";
try
{
    await File.WriteAllTextAsync(tmpManifest,
        JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
    File.Move(tmpManifest, manifestFile, overwrite: true);
}
finally { if (File.Exists(tmpManifest)) File.Delete(tmpManifest); }
Console.WriteLine($"VERIFIED_ARCHIVE_MANIFEST={manifestFile}");
return 0;
