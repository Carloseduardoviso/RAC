using System.Text.Json;

namespace RAC.Infrastructure;

public static class DanielResearchEvidence
{
    public static string Classify(string? json, int candidateCount)
    {
        if(candidateCount is <0 or >20) throw new ArgumentOutOfRangeException(nameof(candidateCount));
        if(json is null) return "LEGACY_COVERAGE_UNKNOWN";
        using var document=JsonDocument.Parse(json);
        var root=document.RootElement;
        if(!root.TryGetProperty("Schema",out var schema)) return "LEGACY_COVERAGE_UNKNOWN";
        if(schema.GetString()!="DANIEL_COVERAGE_V1") throw new FormatException("Unknown coverage schema.");
        var expected=root.GetProperty("ExpectedMarkets").GetInt32();
        var accepted=root.GetProperty("Snapshot").GetProperty("Decisions").GetArrayLength();
        var missing=root.GetProperty("MissingTickers").GetArrayLength();
        var rejected=root.GetProperty("RejectedTickers").GetArrayLength();
        if(expected<=0 || (long)accepted+missing+rejected!=expected || candidateCount>accepted)
            throw new FormatException("Inconsistent coverage counts.");
        if(accepted==0) return "NO_VALID_OBSERVATIONS";
        if(missing>0 || rejected>0) return "PARTIAL_COVERAGE";
        return candidateCount==0 ? "NO_RESEARCH_CANDIDATES" : "RESEARCH_CANDIDATES_AVAILABLE";
    }
}
