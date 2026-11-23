using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using RAC.Application;
using RAC.Domain;

/// <summary>
/// Three fixed, consecutive, completed UTC months. Only verified public archive
/// files on the isolated research drive. Baseline and 2x execution-cost stress
/// are independently re-simulated and selected only on their own training sets.
/// Zero operational DB, account or order dependencies.
/// </summary>
internal static class ThreeMonthResearchCli
{
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4 || args[0] != "--three-months" ||
            !DateOnly.TryParseExact(args[1] + "-01", "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var first) ||
            !DateOnly.TryParseExact(args[2] + "-01", "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var second) ||
            !DateOnly.TryParseExact(args[3] + "-01", "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var third) ||
            second != first.AddMonths(1) || third != second.AddMonths(1) ||
            first < new DateOnly(2017, 8, 1) ||
            third >= new DateOnly(DateTimeOffset.UtcNow.Year,
                DateTimeOffset.UtcNow.Month, 1))
        {
            Console.Error.WriteLine("Usage: --three-months YYYY-MM YYYY-MM YYYY-MM (consecutive completed months)");
            return 2;
        }
        var months = new[] { first, second, third };
        var root = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "RAC", "research", "monthly");
        var completed = new List<MonthlyWalkForwardResearch.Result>();
        var stressed = new List<MonthlyWalkForwardResearch.Result>();
        foreach (var month in months)
        {
            var monthText = month.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var monthDir = Path.Combine(root, monthText);
            var manifestPath = Path.Combine(monthDir, "manifest.json");
            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Validated month not imported.", manifestPath);
            using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
            var header = manifest.RootElement;
            if (header.GetProperty("mode").GetString() != "ISOLATED_MONTHLY_ARCHIVE_RESEARCH_ONLY" ||
                header.GetProperty("month").GetString() != monthText ||
                header.GetProperty("symbols").GetInt32() != RacMarketSymbols.Supported.Length ||
                header.GetProperty("paperModified").GetBoolean() ||
                header.GetProperty("orderPlacementEnabled").GetBoolean() ||
                header.GetProperty("promotionAllowed").GetBoolean())
                throw new InvalidDataException("Unexpected or unsafe archive manifest.");
            var entries = header.GetProperty("reports").EnumerateArray().ToArray();
            if (entries.Length != RacMarketSymbols.Supported.Length ||
                entries.Select(x => x.GetProperty("symbol").GetString())
                    .Distinct(StringComparer.Ordinal).Count() != entries.Length)
                throw new InvalidDataException("Missing or duplicate symbols in manifest.");
            foreach (var symbol in RacMarketSymbols.Supported)
            {
                var item = entries.Single(x => x.GetProperty("symbol").GetString() == symbol);
                var name = $"{symbol}-1m-{monthText}.zip";
                if (item.GetProperty("archiveFile").GetString() != name)
                    throw new InvalidDataException("Archive name differs from manifest.");
                var path = Path.Combine(monthDir, name);
                var info = new FileInfo(path);
                if (!info.Exists || info.Length <= 0 ||
                    info.Length > MonthlyArchiveResearch.MaximumCompressedBytes)
                    throw new InvalidDataException("Archive is missing or exceeds bound.");
                var bytes = await File.ReadAllBytesAsync(path);
                if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)),
                    item.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Archive checksum mismatch.");
                using var baseStream = new MemoryStream(bytes, false);
                var ordinary = MonthlyWalkForwardResearch.Run(symbol, month.Year,
                    month.Month, baseStream, 1m);
                baseStream.Position = 0;
                var doubleCosts = MonthlyWalkForwardResearch.Run(symbol, month.Year,
                    month.Month, baseStream, 2m);
                if (ordinary.Status != "COMPLETE" || doubleCosts.Status != "COMPLETE" ||
                    ordinary.TestedDays != DateTime.DaysInMonth(month.Year, month.Month) ||
                    doubleCosts.TestedDays != ordinary.TestedDays)
                    throw new InvalidDataException("Failed monthly simulation validation.");
                completed.Add(ordinary);
                stressed.Add(doubleCosts);
                Console.WriteLine($"VALIDATED_3M {monthText} {symbol} base={ordinary.MeanSelectedTestReturnPct}% stress={doubleCosts.MeanSelectedTestReturnPct}% days={ordinary.TestedDays}");
            }
        }
        var report = ThreeMonthResearchSummary.Build(months, completed, stressed);
        var folder = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "RAC", "research", "comparisons");
        Directory.CreateDirectory(folder);
        var output = Path.Combine(folder, $"three-month-{args[1]}-to-{args[3]}.json");
        var temporary = output + ".partial";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(report,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, output, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        Console.WriteLine($"THREE_MONTH_RESEARCH_SAVED={output}");
        return 0;
    }
}
