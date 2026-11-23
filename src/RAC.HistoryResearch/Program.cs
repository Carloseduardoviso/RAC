using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using RAC.Application;
using RAC.Domain;

// Manual invocation only. No API registration, worker, trading state or secrets.
if (args.Length > 0 && args[0] == "--entry-gates-summary")
    return await EntryGateCrossMonthCli.RunAsync(args);
if (args.Length > 0 && args[0] == "--entry-gates")
    return await EntryGateResearchCli.RunAsync(args);
if (args.Length > 0 && args[0] == "--three-months")
    return await ThreeMonthResearchCli.RunAsync(args);
// Example: dotnet run --project src/RAC.HistoryResearch -- --backtest 2026-10
if (args.Length == 2 && args[0] == "--backtest")
{
    if (!DateOnly.TryParseExact(args[1] + "-01", "yyyy-MM-dd",
        CultureInfo.InvariantCulture, DateTimeStyles.None, out var backtestMonth))
    {
        Console.Error.WriteLine("Usage: --backtest YYYY-MM (validated downloaded month)");
        return 2;
    }
    var backtestRoot = Path.Combine(Environment.GetFolderPath(
        Environment.SpecialFolder.LocalApplicationData), "RAC", "research", "monthly",
        backtestMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture));
    var manifestPath = Path.Combine(backtestRoot, "manifest.json");
    if (!File.Exists(manifestPath))
        throw new FileNotFoundException("Validated archive manifest missing.", manifestPath);
    using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
    var manifestRoot = document.RootElement;
    if (manifestRoot.GetProperty("mode").GetString() != "ISOLATED_MONTHLY_ARCHIVE_RESEARCH_ONLY" ||
        manifestRoot.GetProperty("month").GetString() != args[1] ||
        manifestRoot.GetProperty("symbols").GetInt32() != RacMarketSymbols.Supported.Length ||
        manifestRoot.GetProperty("paperModified").GetBoolean() ||
        manifestRoot.GetProperty("orderPlacementEnabled").GetBoolean() ||
        manifestRoot.GetProperty("promotionAllowed").GetBoolean())
        throw new InvalidDataException("Manifest failed safety validation.");
    var entries = manifestRoot.GetProperty("reports").EnumerateArray().ToArray();
    if (entries.Length != RacMarketSymbols.Supported.Length ||
        entries.Select(x => x.GetProperty("symbol").GetString()).Distinct().Count() != entries.Length)
        throw new InvalidDataException("Manifest symbols incomplete or duplicate.");
    var results = new List<MonthlyWalkForwardResearch.Result>();
    foreach (var symbol in RacMarketSymbols.Supported)
    {
        var item = entries.SingleOrDefault(x => x.GetProperty("symbol").GetString() == symbol);
        if (item.ValueKind == JsonValueKind.Undefined)
            throw new InvalidDataException("Missing expected archive for agent.");
        var archiveName = $"{symbol}-1m-{args[1]}.zip";
        if (item.GetProperty("archiveFile").GetString() != archiveName)
            throw new InvalidDataException("Unexpected archive name.");
        var archivePath = Path.Combine(backtestRoot, archiveName);
        var raw = await File.ReadAllBytesAsync(archivePath);
        if (raw.Length > MonthlyArchiveResearch.MaximumCompressedBytes || raw.Length == 0 ||
            !string.Equals(Convert.ToHexString(SHA256.HashData(raw)),
                item.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Archive SHA-256 mismatch; refusing backtest.");
        using var input = new MemoryStream(raw, writable: false);
        var assessed = MonthlyWalkForwardResearch.Run(symbol,
            backtestMonth.Year, backtestMonth.Month, input);
        results.Add(assessed);
        Console.WriteLine($"MONTHLY_OOS {symbol} days={assessed.TestedDays} "+
            $"trades={assessed.CompletedTestTrades} positive={assessed.PositiveTestDays} "+
            $"meanNetPct={assessed.MeanSelectedTestReturnPct} gate={assessed.ResearchGate}");
    }
    var report = new {
        mode = "OFFLINE_MONTHLY_WALK_FORWARD_SHADOW_ONLY",
        month = args[1], createdUtc = DateTimeOffset.UtcNow,
        researchOnly = true, historyPersistedInOperationalDatabase = false,
        paperModified = false, riskLimitsModified = false,
        orderPlacementEnabled = false, promotionAllowed = false,
        count = results.Count, trainedMinutesPerDay = MonthlyWalkForwardResearch.TrainMinutes,
        testedMinutesPerDay = MonthlyWalkForwardResearch.HoldoutMinutes,
        horizonsMinutes = PaperTradeHorizonResearch.Horizons,
        noTradingReturnPct = 0m,
        warning = "Exploratory one-month daily holdouts, retrospective regime labels, simplified fees and stops; no automatic strategy approval or proof of future profit.",
        agents = results
    };
    var output = Path.Combine(backtestRoot, "walk-forward-monthly.json");
    var tmp = output + ".partial";
    try
    {
        await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tmp, output, overwrite: true);
    }
    finally { if (File.Exists(tmp)) File.Delete(tmp); }
    Console.WriteLine("MONTHLY_RESEARCH_SAVED=" + output);
    return 0;
}
// Example import: dotnet run --project src/RAC.HistoryResearch -- 2026-10
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
