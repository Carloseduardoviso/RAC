using System.Security.Cryptography;
using System.Text.Json;

namespace RAC.Application;

// Versioned canonical payload for advisory deduplication, not authorization.
public static class RacAnaAdvisoryFingerprint
{
    public static string Compute(RacAnaAdvisoryResult result)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new {
            Version = 1, result.Status,
            Reasons = result.Reasons.Distinct(StringComparer.Ordinal).OrderBy(x => x,StringComparer.Ordinal).ToArray(),
            EvidenceIds = result.EvidenceIds.Distinct().OrderBy(x => x).ToArray(),
            TradingAuthorized = false
        });
        return Convert.ToHexString(SHA256.HashData(payload));
    }
}
