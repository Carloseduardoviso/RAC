using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using RAC.Application;
using RAC.Domain;

/// <summary>One completed public monthly archive, sampled at a fixed hourly
/// interval. Deterministic and separate from the trading stack.</summary>
internal static class LiquidityAuditCli
{
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 2 || args[0] != "--liquidity-audit" ||
            !DateOnly.TryParseExact(args[1] + "-01", "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var month) ||
            month >= new DateOnly(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1))
        { Console.Error.WriteLine("Usage: --liquidity-audit YYYY-MM (completed imported month)"); return 2; }
        var folder = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "RAC", "research", "monthly", args[1]);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(folder, "manifest.json")));
        var root = manifest.RootElement;
        if (root.GetProperty("mode").GetString() != "ISOLATED_MONTHLY_ARCHIVE_RESEARCH_ONLY" ||
            root.GetProperty("month").GetString() != args[1] ||
            root.GetProperty("symbols").GetInt32() != RacMarketSymbols.Supported.Length ||
            root.GetProperty("paperModified").GetBoolean() ||
            root.GetProperty("orderPlacementEnabled").GetBoolean() ||
            root.GetProperty("promotionAllowed").GetBoolean())
            throw new InvalidDataException("Unsafe archive manifest.");
        var entries = root.GetProperty("reports").EnumerateArray().ToArray();
        if (entries.Length != RacMarketSymbols.Supported.Length ||
            entries.Select(x => x.GetProperty("symbol").GetString()).Distinct().Count() != entries.Length)
            throw new InvalidDataException("Incomplete archive manifest.");
        var agents = new List<object>();
        foreach (var symbol in RacMarketSymbols.Supported)
        {
            var item = entries.Single(x => x.GetProperty("symbol").GetString() == symbol);
            var name = $"{symbol}-1m-{args[1]}.zip";
            if (item.GetProperty("archiveFile").GetString() != name)
                throw new InvalidDataException("Archive name mismatch.");
            var bytes = await File.ReadAllBytesAsync(Path.Combine(folder, name));
            if (bytes.Length == 0 || bytes.Length > MonthlyArchiveResearch.MaximumCompressedBytes ||
                !string.Equals(Convert.ToHexString(SHA256.HashData(bytes)),
                    item.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Public archive checksum mismatch.");
            using var stream = new MemoryStream(bytes, writable: false);
            var validation = MonthlyArchiveResearch.Analyze(symbol, month.Year, month.Month, stream);
            stream.Position = 0;
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            using var reader = new StreamReader(zip.Entries.Single().Open());
            var count = 0;
            var observations = 0;
            var zeroFills = 0;
            var partialFills = 0;
            var totalBaseCostPct = 0m;
            var totalStressCostPct = 0m;
            var totalFillFraction = 0m;
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (count % 60 == 0)
                {
                    var values = line.Split(',');
                    if (values.Length != 12) throw new InvalidDataException("Broken validated row.");
                    decimal V(int i) => decimal.Parse(values[i],
                        NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
                    var baseCase = PaperLiquidityStressAudit.Assess(V(1), V(2), V(3),
                        V(5), V(7), 100m, 0.001m, 1m);
                    var stress = PaperLiquidityStressAudit.Assess(V(1), V(2), V(3),
                        V(5), V(7), 100m, 0.001m, 2m);
                    if (stress.FilledNotional != baseCase.FilledNotional ||
                        stress.EstimatedRoundTripCostPct < baseCase.EstimatedRoundTripCostPct)
                        throw new InvalidDataException("Cost stress invariant failed.");
                    observations++;
                    if (baseCase.FilledNotional == 0m) zeroFills++;
                    else if (baseCase.InsufficientLiquidity) partialFills++;
                    totalBaseCostPct += baseCase.EstimatedRoundTripCostPct;
                    totalStressCostPct += stress.EstimatedRoundTripCostPct;
                    totalFillFraction += baseCase.FillFraction;
                }
                count++;
            }
            if (count != validation.ValidatedCandles || observations * 60 != count)
                throw new InvalidDataException("Missing hourly research observations.");
            agents.Add(new { symbol, observations, zeroFills, partialFills,
                meanBaseRoundTripCostPct = Math.Round(totalBaseCostPct / observations, 6),
                meanDoubleCostRoundTripPct = Math.Round(totalStressCostPct / observations, 6),
                meanHypotheticalFillFraction = Math.Round(totalFillFraction / observations, 6),
                archiveSha256 = item.GetProperty("sha256").GetString() });
            Console.WriteLine($"LIQUIDITY_AUDIT {symbol} hourly={observations} zero={zeroFills} partial={partialFills} baseCostPct={totalBaseCostPct / observations:F4} doubleCostPct={totalStressCostPct / observations:F4}");
        }
        var report = new {
            mode = "HOURLY_KLINE_CAPACITY_STRESS_SHADOW_ONLY", month = args[1],
            source = "BINANCE_PUBLIC_MONTHLY_KLINES", count = agents.Count,
            requestedNotionalUsdt = 100m, maxCandleVolumeParticipation = 0.001m,
            observationsPerSymbol = DateTime.DaysInMonth(month.Year, month.Month) * 24,
            scenarios = new[] { 1, 2 }, historicalOrderBookAvailable = false,
            actualFillProbabilityKnown = false, modelOnly = true,
            paperModified = false, riskLimitsModified = false,
            orderPlacementEnabled = false, promotionAllowed = false,
            warning = "OHLCV volume is not executable depth. Estimates only; partial-fill capacity and variable range penalties are hypothetical. Costs exclude latent and queue effects; NOT proof of profitability.",
            agents
        };
        var output = Path.Combine(folder, "liquidity-capacity-audit.json");
        var tmp = output + ".partial";
        try { await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(report,
            new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, output, overwrite: true); }
        finally { if (File.Exists(tmp)) File.Delete(tmp); }
        Console.WriteLine("LIQUIDITY_AUDIT_REPORT=" + output);
        return 0;
    }
}
