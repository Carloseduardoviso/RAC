using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using RAC.Application;
using RAC.Domain;

/// <summary>Offline public OHLCV exit-replay sensitivity. This is not a
/// strategy backtest and does not represent actual order book fills.</summary>
internal static class ExitLiquidityAuditCli
{
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 2 || args[0] != "--exit-liquidity-audit" ||
            !DateOnly.TryParseExact(args[1] + "-01", "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var month) ||
            month >= new DateOnly(DateTimeOffset.UtcNow.Year,
                DateTimeOffset.UtcNow.Month, 1))
        {
            Console.Error.WriteLine("Usage: --exit-liquidity-audit YYYY-MM (completed imported month)");
            return 2;
        }
        var folder = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "RAC", "research",
            "monthly", args[1]);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(folder, "manifest.json")));
        var root = manifest.RootElement;
        if (root.GetProperty("mode").GetString() != "ISOLATED_MONTHLY_ARCHIVE_RESEARCH_ONLY" ||
            root.GetProperty("month").GetString() != args[1] ||
            root.GetProperty("symbols").GetInt32() != RacMarketSymbols.Supported.Length ||
            root.GetProperty("paperModified").GetBoolean() ||
            root.GetProperty("orderPlacementEnabled").GetBoolean() ||
            root.GetProperty("promotionAllowed").GetBoolean())
            throw new InvalidDataException("Untrusted research manifest.");
        var refs = root.GetProperty("reports").EnumerateArray().ToArray();
        if (refs.Length != RacMarketSymbols.Supported.Length ||
            refs.Select(x => x.GetProperty("symbol").GetString()).Distinct().Count() != refs.Length)
            throw new InvalidDataException("Missing or duplicated market symbols.");
        var results = new List<object>();
        foreach (var symbol in RacMarketSymbols.Supported)
        {
            var match = refs.Single(x => x.GetProperty("symbol").GetString() == symbol);
            var name = $"{symbol}-1m-{args[1]}.zip";
            if (match.GetProperty("archiveFile").GetString() != name)
                throw new InvalidDataException("Wrong archive identity.");
            var bytes = await File.ReadAllBytesAsync(Path.Combine(folder, name));
            if (bytes.Length == 0 || bytes.Length > MonthlyArchiveResearch.MaximumCompressedBytes ||
                !string.Equals(Convert.ToHexString(SHA256.HashData(bytes)),
                    match.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Research archive checksum mismatch.");
            using var stream = new MemoryStream(bytes, writable: false);
            var validated = MonthlyArchiveResearch.Analyze(symbol, month.Year,
                month.Month, stream);
            stream.Position = 0;
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            using var csv = new StreamReader(zip.Entries.Single().Open());
            int processed = 0, scenarios = 0, incompleteBase = 0, incompleteStress = 0;
            int noFillBase = 0, partialBase = 0, fullBase = 0;
            decimal unresolvedQuantityBase = 0m, proceedsBase = 0m, proceedsStress = 0m;
            var hourly = new List<MarketCandlePoint>(5);
            string? line;
            while ((line = csv.ReadLine()) is not null)
            {
                var cols = line.Split(',');
                if (cols.Length != 12) throw new InvalidDataException("Malformed candle.");
                if (processed % 60 < 5)
                {
                    var time = long.Parse(cols[0], CultureInfo.InvariantCulture);
                    var openUtc = DateTimeOffset.FromUnixTimeMilliseconds(time >
                        100_000_000_000_000L ? time / 1000L : time);
                    decimal Number(int n) => decimal.Parse(cols[n],
                        NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
                    hourly.Add(new MarketCandlePoint(symbol, openUtc,
                        openUtc.AddMinutes(1).AddMilliseconds(-1),
                        Number(1), Number(2), Number(3), Number(4), Number(5),
                        Number(7), long.Parse(cols[8], CultureInfo.InvariantCulture)));
                    if (hourly.Count == 5)
                    {
                        // Hypothetical 100 USDT of inventory to unwind within
                        // the five candles, using an OHLCV volume proxy.
                        var quantity = 100m / hourly[0].Open;
                        var baseRun = PaperExitLiquidityAudit.Replay(quantity,
                            hourly, 0.001m, 1m);
                        var stressRun = PaperExitLiquidityAudit.Replay(quantity,
                            hourly, 0.001m, 2m);
                        scenarios++;
                        if (!baseRun.AllSold) incompleteBase++;
                        if (!stressRun.AllSold) incompleteStress++;
                        if (baseRun.QuantitySold == 0m) noFillBase++;
                        else if (!baseRun.AllSold) partialBase++;
                        else fullBase++;
                        unresolvedQuantityBase += baseRun.QuantityUnresolved;
                        proceedsBase += baseRun.NetCashRealized;
                        proceedsStress += stressRun.NetCashRealized;
                        if (stressRun.NetCashRealized > baseRun.NetCashRealized ||
                            stressRun.QuantityUnresolved != baseRun.QuantityUnresolved)
                            throw new InvalidDataException("Invalid stress comparison.");
                        hourly.Clear();
                    }
                }
                processed++;
            }
            if (processed != validated.ValidatedCandles || hourly.Count != 0 ||
                scenarios != DateTime.DaysInMonth(month.Year, month.Month) * 24)
                throw new InvalidDataException("Incomplete historical replay.");
            results.Add(new { symbol, scenarios, incompleteBase, incompleteStress,
                noFillBase, partialBase, fullBase,
                unresolvedQuantityBase = Math.Round(unresolvedQuantityBase, 8),
                meanRealizedCashBaseUsdt = Math.Round(proceedsBase / scenarios, 6),
                meanRealizedCashStressUsdt = Math.Round(proceedsStress / scenarios, 6),
                archiveSha256 = match.GetProperty("sha256").GetString() });
            Console.WriteLine($"EXIT_AUDIT {symbol} scenarios={scenarios} incomplete={incompleteBase} noFill={noFillBase} partial={partialBase} full={fullBase}");
        }
        var report = new {
            mode = "FIVE_CANDLE_PARTIAL_EXIT_SHADOW_ONLY", month = args[1],
            createdUtc = DateTimeOffset.UtcNow, count = results.Count,
            scenarioSizeUsdt = 100m, candleWindowMinutes = 5,
            maxVolumeParticipation = 0.001m, hypotheticalModel = true,
            actualOrderBookDepthKnown = false, actualExchangeFillsKnown = false,
            paperModified = false, orderPlacementEnabled = false,
            riskLimitsModified = false, promotionAllowed = false,
            warning = "This is exit feasibility risk research, NOT a strategy PnL or actual order book reconstruction. Unresolved inventory is NOT realized cash; capped simulated liquidity is not a fill promise.",
            agents = results
        };
        var output = Path.Combine(folder, "exit-liquidity-audit.json");
        var temp = output + ".partial";
        try
        {
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(report,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, output, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        Console.WriteLine("EXIT_AUDIT_REPORT=" + output);
        return 0;
    }
}
