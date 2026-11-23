using System.Text.Json;
using RAC.Application;

/// <summary>Manual offline SHADOW report; no access to SQL or Binance orders.</summary>
internal static class SixMonthValidationCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 1 || args[0] != "--six-month-validation") return 2;
        var root = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "RAC", "research", "monthly");
        var report = RacShadowSixMonthValidation.Load(root);
        if (!report.Available)
        {
            Console.Error.WriteLine(report.Notice + " Missing: " +
                string.Join(", ", report.MissingMonths));
            return 3;
        }
        foreach (var month in report.Timeline)
            Console.WriteLine($"SIX_MONTH {month.MonthKey} windows={month.AgentDays} " +
                $"noTrade={month.NoTradeDays} trades={month.HypotheticalTrades} " +
                $"unresolved={month.UnresolvedExits}");
        var output = Path.Combine(root, "six-month-validation-2026-05-to-2026-10.json");
        var partial = output + ".partial";
        try
        {
            await File.WriteAllTextAsync(partial, JsonSerializer.Serialize(report,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(partial, output, overwrite: true);
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
        Console.WriteLine($"SIX_MONTH_SUMMARY agents={report.Agents} " +
            $"days={report.AgentDays} noTrade={report.NoTradeDays} " +
            $"trades={report.HypotheticalTrades} unresolved={report.UnresolvedExits} " +
            $"statisticallySupported={report.StatisticallySupportedAgents} " +
            $"reviewEligible={report.AgentsEligibleForResearchReview} " +
            $"paper={report.PaperModified} orders={report.OrderPlacementEnabled} " +
            $"promotion={report.PromotionAllowed}");
        Console.WriteLine("SIX_MONTH_REPORT=" + output);
        return 0;
    }
}
