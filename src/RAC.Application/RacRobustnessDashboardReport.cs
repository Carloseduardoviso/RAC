using System.Text.Json;
using RAC.Domain;

namespace RAC.Application;

/// <summary>
/// Validates an isolated, generated SHADOW report before making a small
/// read-only projection available to authenticated dashboard users.
/// No execution code, credentials, or operational database dependencies.
/// </summary>
public static class RacRobustnessDashboardReport
{
    public sealed record Result(bool Available, string Mode,
        int Agents, int HoldoutMonths, int AbstainingAgents,
        int InsufficientEvidenceAgents, int UnstableAgents,
        bool PaperModified, bool OrderPlacementEnabled, bool PromotionAllowed,
        IReadOnlyList<RacCandidateRobustnessScreen.Row> Rows, string Notice);

    public static Result Load(string monthlyRoot)
    {
        var path = Path.Combine(monthlyRoot, "fixed-replay-2026-08-to-2026-10.json");
        if (!File.Exists(path))
            return Unavailable("Relatório de replay fixo ainda não disponível neste computador.");
        var info = new FileInfo(path);
        if (info.Length is < 100 or > 300_000)
            throw new InvalidDataException("Unexpected robustness report size.");
        var frozen = RacFrozenDecisionAudit.Load(monthlyRoot);
        if (!frozen.Available)
            return Unavailable("Auditoria do treinamento incompleta.");
        return Parse(File.ReadAllText(path), frozen);
    }

    public static Result Parse(string json, RacFrozenDecisionAudit.Result frozen)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 20 });
        var root = doc.RootElement;
        if (root.GetProperty("mode").GetString() !=
                "FIXED_POST_TRAINING_SHADOW_ONLY" ||
            root.GetProperty("paperModified").GetBoolean() ||
            root.GetProperty("orderPlacementEnabled").GetBoolean() ||
            root.GetProperty("promotionAllowed").GetBoolean() ||
            root.GetProperty("agents").GetInt32() != RacMarketSymbols.Supported.Length ||
            !MonthsMatch(root.GetProperty("trainingMonths"),
                RacFrozenDecisionAudit.TrainMonths) ||
            !MonthsMatch(root.GetProperty("holdoutMonths"),
                RacFrozenDecisionAudit.HoldoutMonths))
            throw new InvalidDataException("Unsafe fixed replay metadata.");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var rows = root.GetProperty("rows").Deserialize<RacFixedHoldoutReplay.Result[]>(options)
            ?? throw new InvalidDataException("No fixed replay rows.");
        if (rows.Length != 36)
            throw new InvalidDataException("Incomplete fixed replay report.");
        // Derive the projection from validated data, never trust the
        // report's embedded robustness numbers as authorization evidence.
        var result = RacCandidateRobustnessScreen.Evaluate(frozen, rows);
        if (result.Rows.Count != 12 || result.PromotionAllowed ||
            result.OrderPlacementEnabled || result.PaperModified)
            throw new InvalidDataException("Unsafe robustness projection.");
        return new(true, result.Mode, result.Agents, 3,
            result.AbstainingAgents, result.InsufficientEvidenceAgents,
            result.UnstableAgents, false, false, false, result.Rows,
            result.Notice);
    }

    private static bool MonthsMatch(JsonElement item, string[] expected) =>
        item.ValueKind == JsonValueKind.Array &&
        item.EnumerateArray().Select(x => x.GetString()).SequenceEqual(expected);

    private static Result Unavailable(string notice) =>
        new(false, "ROBUSTNESS_NOT_AVAILABLE", 0, 0, 0, 0, 0,
            false, false, false, [], notice);
}
