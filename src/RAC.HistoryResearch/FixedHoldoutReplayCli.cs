using System.Security.Cryptography;
using System.Text.Json;
using RAC.Application;

/// <summary>Manual research-only replay of frozen decisions on later monthly archives.</summary>
internal static class FixedHoldoutReplayCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 1 || args[0] != "--fixed-holdout-replay") return 2;
        var root = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "RAC", "research", "monthly");
        var frozen = RacFrozenDecisionAudit.Load(root);
        if (!frozen.Available || frozen.Agents != 12 ||
            frozen.PromotionAllowed || frozen.OrderPlacementEnabled)
            throw new InvalidDataException("No safe pre-holdout frozen decisions.");
        var results = new List<RacFixedHoldoutReplay.Result>();
        foreach (var month in RacFrozenDecisionAudit.HoldoutMonths)
        {
            var folder = Path.Combine(root, month);
            using var manifestDoc = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(folder, "manifest.json")),
                new JsonDocumentOptions { MaxDepth = 16 });
            var manifest = manifestDoc.RootElement;
            if (manifest.GetProperty("mode").GetString() !=
                    "ISOLATED_MONTHLY_ARCHIVE_RESEARCH_ONLY" ||
                manifest.GetProperty("month").GetString() != month ||
                manifest.GetProperty("source").GetString() !=
                    "https://data.binance.vision/data/spot/monthly/klines/" ||
                manifest.GetProperty("symbols").GetInt32() != 12 ||
                manifest.GetProperty("paperModified").GetBoolean() ||
                manifest.GetProperty("orderPlacementEnabled").GetBoolean() ||
                manifest.GetProperty("promotionAllowed").GetBoolean())
                throw new InvalidDataException("Untrusted holdout manifest.");
            var entries = manifest.GetProperty("reports").EnumerateArray().ToArray();
            if (entries.Length != 12 ||
                entries.Select(x => x.GetProperty("symbol").GetString())
                    .Distinct(StringComparer.Ordinal).Count() != 12)
                throw new InvalidDataException("Incomplete holdout manifest.");
            foreach (var decision in frozen.Rows)
            {
                var item = entries.Single(x => x.GetProperty("symbol").GetString() == decision.Symbol);
                var filename = decision.Symbol + "-1m-" + month + ".zip";
                if (item.GetProperty("archiveFile").GetString() != filename)
                    throw new InvalidDataException("Unexpected holdout archive.");
                var expected = item.GetProperty("sha256").GetString();
                if (expected is null || expected.Length != 64 ||
                    !expected.All(Uri.IsHexDigit))
                    throw new InvalidDataException("Missing trusted archive hash.");
                var path = Path.Combine(folder, filename);
                using var archive = File.OpenRead(path);
                if (archive.Length is <= 0 or > MonthlyArchiveResearch.MaximumCompressedBytes ||
                    !string.Equals(Convert.ToHexString(SHA256.HashData(archive)),
                        expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Holdout archive digest mismatch.");
                archive.Position = 0;
                var replay = RacFixedHoldoutReplay.Evaluate(decision.Symbol,
                    int.Parse(month[..4]), int.Parse(month[5..]), archive,
                    decision.FrozenEntryGate, decision.FrozenHorizonMinutes);
                if (replay.ReviewEligible || replay.PromotionAllowed ||
                    replay.OrderPlacementEnabled || replay.PaperModified)
                    throw new InvalidDataException("Unsafe fixed replay output.");
                results.Add(replay);
                Console.WriteLine($"FIXED {month} {decision.Symbol} status={replay.EvaluationStatus} " +
                    $"trades={replay.ClosedTrades} unresolved={replay.UnresolvedExitIntents}");
            }
        }
        var diagnostic = RacFixedCandidateDiagnostics.Compare(frozen, results);
        var payload = new {
            mode = "FIXED_POST_TRAINING_SHADOW_ONLY",
            diagnostic,
            trainingMonths = RacFrozenDecisionAudit.TrainMonths,
            holdoutMonths = RacFrozenDecisionAudit.HoldoutMonths,
            agents = frozen.Agents,
            rows = results,
            paperModified = false, orderPlacementEnabled = false,
            promotionAllowed = false,
            notice = "Frozen from May-July only. NO_TRADE is an abstention baseline, " +
                "not an active strategy return. Active replays are hypothetical with " +
                "illustrative fills and capacity checks; no strategy approval."
        };
        var destination = Path.Combine(root, "fixed-replay-2026-08-to-2026-10.json");
        var partial = destination + ".partial";
        try
        {
            await File.WriteAllTextAsync(partial, JsonSerializer.Serialize(payload,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(partial, destination, overwrite: true);
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
        Console.WriteLine($"FIXED_SUMMARY rows={results.Count} noTrade=" +
            results.Count(x => x.FrozenGate == "NO_TRADE") +
            " reviewEligible=0 paperModified=false ordersEnabled=false");
        return 0;
    }
}
