using System.Security.Cryptography;
using System.Text.Json;
using RAC.Application;
using RAC.Domain;

/// <summary>Manual read-only audit of local Binance public archive manifests.</summary>
internal static class ArchiveProvenanceCli
{
    public static int Run(string[] args)
    {
        if (args.Length != 1 || args[0] != "--archive-provenance") return 2;
        var root = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "RAC", "research", "monthly");
        try
        {
            var total = 0;
            foreach (var month in RacFrozenDecisionAudit.TrainMonths.Concat(
                RacFrozenDecisionAudit.HoldoutMonths))
            {
                var directory = Path.Combine(root, month);
                var manifestPath = Path.Combine(directory, "manifest.json");
                using var document = JsonDocument.Parse(File.ReadAllText(manifestPath),
                    new JsonDocumentOptions { MaxDepth = 16 });
                var manifest = document.RootElement;
                if (manifest.GetProperty("mode").GetString() !=
                        "ISOLATED_MONTHLY_ARCHIVE_RESEARCH_ONLY" ||
                    manifest.GetProperty("month").GetString() != month ||
                    manifest.GetProperty("source").GetString() !=
                        "https://data.binance.vision/data/spot/monthly/klines/" ||
                    manifest.GetProperty("symbols").GetInt32() != RacMarketSymbols.Supported.Length ||
                    manifest.GetProperty("paperModified").GetBoolean() ||
                    manifest.GetProperty("orderPlacementEnabled").GetBoolean() ||
                    manifest.GetProperty("promotionAllowed").GetBoolean())
                    throw new InvalidDataException("Invalid research manifest: " + month);
                var reports = manifest.GetProperty("reports").EnumerateArray().ToArray();
                if (reports.Length != RacMarketSymbols.Supported.Length ||
                    reports.Select(x => x.GetProperty("symbol").GetString())
                        .Distinct(StringComparer.Ordinal).Count() != reports.Length)
                    throw new InvalidDataException("Manifest inventory mismatch: " + month);
                foreach (var symbol in RacMarketSymbols.Supported)
                {
                    var item = reports.Single(x => x.GetProperty("symbol").GetString() == symbol);
                    var filename = symbol + "-1m-" + month + ".zip";
                    var digest = item.GetProperty("sha256").GetString();
                    if (item.GetProperty("archiveFile").GetString() != filename ||
                        digest is null || digest.Length != 64 ||
                        !digest.All(Uri.IsHexDigit))
                        throw new InvalidDataException("Unsafe archive reference: " + month + "/" + symbol);
                    var path = Path.Combine(directory, filename);
                    var info = new FileInfo(path);
                    if (!info.Exists || info.Length is <= 0 or > MonthlyArchiveResearch.MaximumCompressedBytes)
                        throw new InvalidDataException("Archive missing or oversized: " + month + "/" + symbol);
                    using var stream = File.OpenRead(path);
                    var actual = Convert.ToHexString(SHA256.HashData(stream));
                    if (!string.Equals(actual, digest, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Archive SHA-256 mismatch: " + month + "/" + symbol);
                    total++;
                }
                Console.WriteLine("PROVENANCE_OK month=" + month + " archives=12");
            }
            Console.WriteLine("PROVENANCE_SUMMARY checked=" + total +
                " localManifestMatches=true independentBinanceSignatureVerified=false" +
                " paperModified=false ordersEnabled=false");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            InvalidDataException or JsonException or KeyNotFoundException or
            InvalidOperationException)
        {
            Console.Error.WriteLine("PROVENANCE_FAILED " + ex.GetType().Name +
                ": " + ex.Message);
            return 3;
        }
    }
}
