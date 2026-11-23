using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using RAC.Application;
using RAC.Domain;

/// <summary>Manual, isolated research over one fully validated public month.
/// Neither the source archive nor any operational state is changed.</summary>
internal static class EntryGateStressCli
{
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 2 || args[0] != "--entry-gates-stress" ||
            !DateOnly.TryParseExact(args[1] + "-01", "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var month) ||
            month >= new DateOnly(DateTimeOffset.UtcNow.Year, DateTimeOffset.UtcNow.Month, 1))
        {
            Console.Error.WriteLine("Usage: --entry-gates-stress YYYY-MM (completed, imported month)");
            return 2;
        }
        var root = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "RAC", "research",
            "monthly", args[1]);
        var manifestPath = Path.Combine(root, "manifest.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        var manifest = document.RootElement;
        if (manifest.GetProperty("mode").GetString() != "ISOLATED_MONTHLY_ARCHIVE_RESEARCH_ONLY" ||
            manifest.GetProperty("month").GetString() != args[1] ||
            manifest.GetProperty("symbols").GetInt32() != RacMarketSymbols.Supported.Length ||
            manifest.GetProperty("paperModified").GetBoolean() ||
            manifest.GetProperty("orderPlacementEnabled").GetBoolean() ||
            manifest.GetProperty("promotionAllowed").GetBoolean())
            throw new InvalidDataException("Unsafe or incorrect public archive manifest.");
        var items = manifest.GetProperty("reports").EnumerateArray().ToArray();
        if (items.Length != RacMarketSymbols.Supported.Length ||
            items.Select(x => x.GetProperty("symbol").GetString()).Distinct().Count() != items.Length)
            throw new InvalidDataException("Missing or duplicated archive symbols.");
        var agents = new List<object>();
        foreach (var symbol in RacMarketSymbols.Supported)
        {
            var item = items.Single(x => x.GetProperty("symbol").GetString() == symbol);
            var name = $"{symbol}-1m-{args[1]}.zip";
            if (item.GetProperty("archiveFile").GetString() != name)
                throw new InvalidDataException("Invalid research archive name.");
            var path = Path.Combine(root, name);
            var info = new FileInfo(path);
            if (!info.Exists || info.Length <= 0 ||
                info.Length > MonthlyArchiveResearch.MaximumCompressedBytes)
                throw new InvalidDataException("Research file missing or too large.");
            var bytes = await File.ReadAllBytesAsync(path);
            if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)),
                item.GetProperty("sha256").GetString(), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Archive SHA-256 mismatch.");
            using var archive = new MemoryStream(bytes, writable: false);
            var baseline = MonthlyWalkForwardResearch.Run(symbol, month.Year,
                month.Month, archive, 2m);
            archive.Position = 0;
            var gated = MonthlyWalkForwardResearch.Run(symbol, month.Year,
                month.Month, archive, 2m, entryGateResearch: true);
            if (baseline.TestedDays != gated.TestedDays ||
                baseline.TestedDays != DateTime.DaysInMonth(month.Year, month.Month) ||
                baseline.Status != "COMPLETE" || gated.Status != "COMPLETE" ||
                gated.PaperModified || gated.OrderPlacementEnabled || gated.PromotionAllowed ||
                gated.Days.Count != baseline.Days.Count)
                throw new InvalidDataException("Entry gate results incomplete or unsafe.");
            var inactive = gated.Days.Count(x => x.SelectedEntryGate == "NO_TRADE");
            var active = gated.Days.Count - inactive;
            var gateResult = gated.PositiveTestDays >= (gated.TestedDays * 7 + 9) / 10 &&
                gated.CompletedTestTrades >= 30 &&
                gated.MeanSelectedTestReturnPct > 0m &&
                gated.MeanSelectedTestReturnPct > baseline.MeanSelectedTestReturnPct &&
                gated.WorstSelectedTestDrawdownPct <= 5m && active >= 8
                ? "RESEARCH_REVIEW_ONLY" : "REJECTED_OR_INSUFFICIENT_EDGE";
            Console.WriteLine($"STRESSED_ENTRY_GATE {symbol} holdouts={gated.TestedDays} noTrade={inactive} trading={active} trades={gated.CompletedTestTrades} meanNet={gated.MeanSelectedTestReturnPct}% baseline={baseline.MeanSelectedTestReturnPct}% review={gateResult}");
            agents.Add(new {
                symbol, gateResult, totalDays = gated.TestedDays,
                noTradeDays = inactive, activeDays = active,
                selectedClosedTrades = gated.CompletedTestTrades,
                baselineClosedTrades = baseline.CompletedTestTrades,
                netMeanPct = gated.MeanSelectedTestReturnPct,
                baselineMeanNetPct = baseline.MeanSelectedTestReturnPct,
                positiveDays = gated.PositiveTestDays,
                worstDrawdownPct = gated.WorstSelectedTestDrawdownPct,
                selection = gated.Days.Select(x => new {
                    x.DateUtc, x.SelectedEntryGate, x.ChosenHorizonMinutes,
                    x.TrainingTrades, x.TrainingReturnPct, x.TestTrades,
                    x.SelectedTestReturnPct, x.BaselineTestReturnPct,
                    x.SelectedTestDrawdownPct
                }).ToArray()
            });
        }
        var report = new {
            mode = "ENTRY_QUALITY_NO_TRADE_DOUBLE_COST_SHADOW_ONLY",
            month = args[1], createdUtc = DateTimeOffset.UtcNow,
            count = agents.Count, studyDays = agents.Count * DateTime.DaysInMonth(month.Year, month.Month),
            fixedEntryGates = new[] { "BASELINE", "TREND_STRENGTH", "ATR_COST", "TREND_AND_ATR", "NO_TRADE" },
            holdingHorizonsMinutes = new[] { 15, 30 },
            trainingMinutes = MonthlyWalkForwardResearch.TrainMinutes,
            holdoutMinutes = MonthlyWalkForwardResearch.HoldoutMinutes,
            minimumTrainingClosedTrades = 3,
            baselineNoTradePct = 0m, costMultiplier = 2m,
            noOrderPlacement = true, paperModified = false,
            riskLimitsModified = false, orderPlacementEnabled = false,
            historyPersistedInOperationalDatabase = false, promotionAllowed = false,
            warning = "Exploratory month only; NO_TRADE at zero is favored unless training is positive with support. Holdouts may still lose. No strategy promotion or future-profit guarantee.",
            agents
        };
        var output = Path.Combine(root, "entry-quality-no-trade-double-cost.json");
        var temp = output + ".partial";
        try
        {
            await File.WriteAllTextAsync(temp,
                JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, output, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        Console.WriteLine("STRESSED_ENTRY_GATE_REPORT=" + output);
        return 0;
    }
}
