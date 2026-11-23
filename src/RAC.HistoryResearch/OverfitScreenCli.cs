using System.Text.Json;
using RAC.Application;

/// <summary>Explicit research-only diagnostic. No SQL, brokerage or orders.</summary>
internal static class OverfitScreenCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 1 || args[0] != "--overfit-screen")
        {
            Console.Error.WriteLine("Usage: --overfit-screen");
            return 2;
        }
        var monthly = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "RAC", "research", "monthly");
        var history = RacShadowThreeMonthDashboard.Load(monthly);
        var result = RacShadowOverfitDiagnostics.Assess(history);
        if (!result.Available)
        {
            Console.Error.WriteLine(result.Notice);
            return 3;
        }
        foreach (var row in result.Rows)
            Console.WriteLine($"OVERFIT {row.Symbol} positive={row.PositiveMonths} " +
                $"negative={row.NegativeMonths} flat={row.FlatMonths} " +
                $"trades={row.HypotheticalTrades} unresolved={row.UnresolvedExits} " +
                $"p={row.OneSidedSignP:F6} adjustedP={row.BonferroniAdjustedP:F6} " +
                $"status={row.EvidenceStatus}");
        var output = Path.Combine(monthly, "overfit-screen-2026-08-to-2026-10.json");
        var temp = output + ".partial";
        try
        {
            await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(result,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, output, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        Console.WriteLine($"OVERFIT_SCREEN_SUMMARY agents={result.Agents} " +
            $"positiveAll={result.AgentsWithPositiveEveryMonth} " +
            $"unresolvedAgents={result.AgentsWithUnresolvedExits} " +
            $"statisticallySupported={result.AgentsStatisticallySupported} " +
            $"reviewEligible={result.AgentsEligibleForResearchReview} " +
            $"paper={result.PaperModified} orders={result.OrderPlacementEnabled} " +
            $"promotion={result.PromotionAllowed}");
        Console.WriteLine("OVERFIT_SCREEN_REPORT=" + output);
        return 0;
    }
}
