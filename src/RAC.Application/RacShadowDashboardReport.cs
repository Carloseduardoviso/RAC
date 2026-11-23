using System.Globalization;
using System.Text.Json;
using RAC.Domain;

namespace RAC.Application;

/// <summary>
/// Read-only, bounded projection of an isolated SHADOW research report.
/// Missing or untrusted files never unlock operational execution.
/// </summary>
public static class RacShadowDashboardReport
{
    public sealed record Agent(string Symbol, string Gate, int TotalDays,
        int NoTradeDays, int ActiveDays, int HypotheticalClosedTrades,
        int ExitIntents, int UnresolvedExits,
        int BlockedEntryAttempts, int PartialEntryAttempts,
        decimal HypotheticalMeanNetPct);
    public sealed record Result(bool Available, string Mode, string Month,
        int Agents, int TotalAgentDays, int NoTradeDays, int ActiveDays,
        int HypotheticalTrades, int ExitIntents, int UnresolvedExits,
        int RejectedAgents, int ReviewOnlyAgents,
        bool PaperModified, bool OrderPlacementEnabled,
        bool PromotionAllowed, IReadOnlyList<Agent> Rows, string Notice);

    public static Result Load(string monthlyRoot, string month)
    {
        if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ||
            parsed.ToString("yyyy-MM", CultureInfo.InvariantCulture) != month ||
            parsed >= new DateOnly(DateTimeOffset.UtcNow.Year,
                DateTimeOffset.UtcNow.Month, 1))
            throw new ArgumentException("Invalid or unfinished research month.", nameof(month));
        var file = Path.Combine(monthlyRoot, month,
            "exit-capacity-gated-backtest.json");
        if (!File.Exists(file))
            return new(false, "NOT_IMPORTED", month, 0, 0, 0, 0, 0,
                0, 0, 0, 0, false, false, false, [],
                "Sem relatório SHADOW validado neste computador. Não implica aprovação de estratégias.");
        if (new FileInfo(file).Length is < 20 or > 2_000_000)
            throw new InvalidDataException("Unexpected report size.");
        using var json = JsonDocument.Parse(File.ReadAllText(file),
            new JsonDocumentOptions { MaxDepth = 16 });
        var root = json.RootElement;
        if (root.GetProperty("mode").GetString() != "EXIT_CAPACITY_GATED_SHADOW_ONLY" ||
            root.GetProperty("month").GetString() != month ||
            root.GetProperty("paperModified").GetBoolean() ||
            root.GetProperty("orderPlacementEnabled").GetBoolean() ||
            root.GetProperty("promotionAllowed").GetBoolean() ||
            root.GetProperty("count").GetInt32() != RacMarketSymbols.Supported.Length)
            throw new InvalidDataException("Report fails SHADOW safety verification.");
        var items = root.GetProperty("agents").EnumerateArray().ToArray();
        if (items.Length != RacMarketSymbols.Supported.Length ||
            items.Select(x => x.GetProperty("symbol").GetString())
                .Distinct(StringComparer.Ordinal).Count() != items.Length)
            throw new InvalidDataException("Incorrect agent inventory.");
        var results = new List<Agent>(items.Length);
        foreach (var symbol in RacMarketSymbols.Supported)
        {
            var a = items.SingleOrDefault(x => x.GetProperty("symbol").GetString() == symbol);
            if (a.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Missing required symbol.");
            var gate = a.GetProperty("gateResult").GetString() ?? "";
            if (gate is not ("EXIT_UNRESOLVED_REJECTED" or
                "REJECTED_OR_INSUFFICIENT_EDGE" or "RESEARCH_REVIEW_ONLY"))
                throw new InvalidDataException("Unknown research gate.");
            int I(string key) => a.GetProperty(key).GetInt32();
            var days = I("totalDays");
            var idle = I("noTradeDays");
            var active = I("activeDays");
            var trades = I("selectedClosedTrades");
            var exits = I("exitIntentCount");
            var unresolved = I("exitUnresolvedCount");
            var blocked = I("liquidityBlockedEntries");
            var partial = I("partialEntryFills");
            if (days != DateTime.DaysInMonth(parsed.Year, parsed.Month) ||
                idle < 0 || active < 0 || idle + active != days ||
                trades < 0 || trades != exits || unresolved < 0 ||
                unresolved > exits || blocked < 0 || partial < 0 ||
                (unresolved > 0 && gate != "EXIT_UNRESOLVED_REJECTED"))
                throw new InvalidDataException("Inconsistent research counters.");
            results.Add(new(symbol, gate, days, idle, active, trades, exits,
                unresolved, blocked, partial,
                a.GetProperty("netMeanPct").GetDecimal()));
        }
        return new(true, "EXIT_CAPACITY_GATED_SHADOW_ONLY", month,
            results.Count, results.Sum(x => x.TotalDays),
            results.Sum(x => x.NoTradeDays), results.Sum(x => x.ActiveDays),
            results.Sum(x => x.HypotheticalClosedTrades),
            results.Sum(x => x.ExitIntents), results.Sum(x => x.UnresolvedExits),
            results.Count(x => x.Gate != "RESEARCH_REVIEW_ONLY"),
            results.Count(x => x.Gate == "RESEARCH_REVIEW_ONLY"),
            false, false, false, results,
            "Pesquisa retrospectiva isolada. Taxas, liquidez e fills são hipóteses; retornos são médias de janelas fictícias e não lucros reais. Revisão de pesquisa não autoriza negociação.");
    }
}
