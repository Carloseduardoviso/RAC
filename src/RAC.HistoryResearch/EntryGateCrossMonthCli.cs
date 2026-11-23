using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using RAC.Application;
using RAC.Domain;

/// <summary>Cross-month evidence consolidation. Reads SHADOW JSON research only.</summary>
internal static class EntryGateCrossMonthCli
{
    internal static async Task<int> RunAsync(string[] args)
    {
        var stressed = args.Length > 0 && args[0] == "--entry-gates-stress-summary";
        if (args.Length != 4 ||
            args[0] is not ("--entry-gates-summary" or "--entry-gates-stress-summary") ||
            !DateOnly.TryParseExact(args[1] + "-01", "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var first) ||
            !DateOnly.TryParseExact(args[2] + "-01", "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var second) ||
            !DateOnly.TryParseExact(args[3] + "-01", "yyyy-MM-dd",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var third) ||
            second != first.AddMonths(1) || third != second.AddMonths(1) ||
            third >= new DateOnly(DateTimeOffset.UtcNow.Year,
                DateTimeOffset.UtcNow.Month, 1))
        {
            Console.Error.WriteLine("Usage: --entry-gates-summary or --entry-gates-stress-summary YYYY-MM YYYY-MM YYYY-MM (three completed consecutive months)");
            return 2;
        }
        var keys = args.Skip(1).ToArray();
        var root = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "RAC", "research");
        var all = new List<EntryGateCrossMonthSummary.Month>();
        var fingerprints = new List<object>();
        foreach (var key in keys)
        {
            var path = Path.Combine(root, "monthly", key,
                stressed ? "entry-quality-no-trade-double-cost.json"
                    : "entry-quality-no-trade.json");
            var bytes = await File.ReadAllBytesAsync(path);
            using var document = JsonDocument.Parse(bytes);
            var obj = document.RootElement;
            if (obj.GetProperty("mode").GetString() !=
                    (stressed ? "ENTRY_QUALITY_NO_TRADE_DOUBLE_COST_SHADOW_ONLY"
                        : "ENTRY_QUALITY_NO_TRADE_SHADOW_ONLY") ||
                (stressed && obj.GetProperty("costMultiplier").GetDecimal() != 2m) ||
                obj.GetProperty("month").GetString() != key ||
                obj.GetProperty("count").GetInt32() != RacMarketSymbols.Supported.Length ||
                obj.GetProperty("paperModified").GetBoolean() ||
                obj.GetProperty("riskLimitsModified").GetBoolean() ||
                obj.GetProperty("orderPlacementEnabled").GetBoolean() ||
                obj.GetProperty("promotionAllowed").GetBoolean())
                throw new InvalidDataException("Input is not an authorized SHADOW research report.");
            var items = obj.GetProperty("agents").EnumerateArray().ToArray();
            if (items.Length != RacMarketSymbols.Supported.Length ||
                items.Select(x => x.GetProperty("symbol").GetString()).Distinct().Count() != items.Length)
                throw new InvalidDataException("Duplicate or missing agents.");
            foreach (var item in items)
            {
                var symbol = item.GetProperty("symbol").GetString() ?? "";
                if (!RacMarketSymbols.IsSupported(symbol))
                    throw new InvalidDataException("Unexpected market symbol.");
                var noTrade = item.GetProperty("noTradeDays").GetInt32();
                var active = item.GetProperty("activeDays").GetInt32();
                var tradeCount = item.GetProperty("selectedClosedTrades").GetInt32();
                var positives = item.GetProperty("positiveDays").GetInt32();
                var selection = item.GetProperty("selection").EnumerateArray().ToArray();
                if (selection.Length != item.GetProperty("totalDays").GetInt32() ||
                    selection.Count(x => x.GetProperty("SelectedEntryGate").GetString() == "NO_TRADE") != noTrade ||
                    selection.Sum(x => x.GetProperty("TestTrades").GetInt32()) != tradeCount ||
                    selection.Count(x => x.GetProperty("SelectedTestReturnPct").GetDecimal() > 0m) != positives ||
                    selection.Any(x => x.GetProperty("SelectedEntryGate").GetString() == "NO_TRADE" &&
                        (x.GetProperty("TestTrades").GetInt32() != 0 ||
                         x.GetProperty("SelectedTestReturnPct").GetDecimal() != 0m ||
                         x.GetProperty("SelectedTestDrawdownPct").GetDecimal() != 0m)))
                    throw new InvalidDataException("Daily SHADOW results disagree with totals.");
                if (selection.Select(x => x.GetProperty("DateUtc").GetString()).Distinct().Count() != selection.Length)
                    throw new InvalidDataException("Duplicate test dates.");
                if (selection.Any(x => !x.GetProperty("DateUtc").GetString()!.StartsWith(key, StringComparison.Ordinal)))
                    throw new InvalidDataException("Cross-month test day detected.");
                all.Add(new EntryGateCrossMonthSummary.Month(symbol, key,
                    item.GetProperty("totalDays").GetInt32(), noTrade, active,
                    tradeCount, positives, item.GetProperty("netMeanPct").GetDecimal(),
                    item.GetProperty("baselineMeanNetPct").GetDecimal(),
                    false, false, false));
            }
            fingerprints.Add(new { month = key,
                entryGateReportSha256 = Convert.ToHexString(SHA256.HashData(bytes)) });
        }
        var result = EntryGateCrossMonthSummary.Build(keys, all);
        var outputDir = Path.Combine(root, "comparisons");
        Directory.CreateDirectory(outputDir);
        var output = Path.Combine(outputDir,
            $"entry-quality-three-month-{keys[0]}-to-{keys[2]}{(stressed ? "-double-cost" : "")}.json");
        var temporary = output + ".partial";
        var payload = new {
            Mode = stressed ? "THREE_MONTH_ENTRY_GATES_DOUBLE_COST_SHADOW_ONLY" : result.Mode,
            CostMultiplier = stressed ? 2m : 1m,
            result.AgentCount, result.Months,
            result.TotalAgentDays, result.NoTradeDays, result.ActiveDays,
            result.CompletedTrades, result.PositiveDays, result.RejectedAgents,
            result.ReviewOnlyAgents, result.NoTradeBenchmarkPct,
            result.PaperModified, result.RiskLimitsModified,
            result.OrderPlacementEnabled, result.PromotionAllowed,
            inputEvidence = fingerprints, result.Agents, result.Interpretation,
            createdUtc = DateTimeOffset.UtcNow };
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(payload,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, output, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
        Console.WriteLine($"CROSS_MONTH_GATE days={result.TotalAgentDays} noTrade={result.NoTradeDays} active={result.ActiveDays} trades={result.CompletedTrades} positive={result.PositiveDays} rejected={result.RejectedAgents} review={result.ReviewOnlyAgents} orders={result.OrderPlacementEnabled}");
        Console.WriteLine("CROSS_MONTH_GATE_REPORT=" + output);
        return 0;
    }
}
