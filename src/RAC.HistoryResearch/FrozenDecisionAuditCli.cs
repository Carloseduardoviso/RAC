using System.Text.Json;
using RAC.Application;

/// <summary>Manual, read-only pre-holdout decision freeze.</summary>
internal static class FrozenDecisionAuditCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 1 || args[0] != "--frozen-decision-audit") return 2;
        var root = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "RAC", "research", "monthly");
        var report = RacFrozenDecisionAudit.Load(root);
        if (!report.Available)
        {
            Console.Error.WriteLine(report.Notice + " " +
                string.Join(",", report.MissingMonths));
            return 3;
        }
        foreach (var x in report.Rows)
            Console.WriteLine($"FROZEN {x.Symbol} gate={x.FrozenEntryGate} " +
                $"horizon={x.FrozenHorizonMinutes} votes={x.VotesForFrozen}/{x.TrainingWindows} " +
                $"holdouts={x.OutOfSampleWindows} status={x.EvaluationStatus}");
        var output = Path.Combine(root, "frozen-decision-2026-05-to-2026-10.json");
        var partial = output + ".partial";
        try
        {
            await File.WriteAllTextAsync(partial, JsonSerializer.Serialize(report,
                new JsonSerializerOptions { WriteIndented = true }));
            File.Move(partial, output, overwrite: true);
        }
        finally { if (File.Exists(partial)) File.Delete(partial); }
        Console.WriteLine($"FROZEN_SUMMARY agents={report.Agents} train={report.TrainingWindows} " +
            $"test={report.HoldoutWindows} noTrade={report.FrozenNoTradeAgents} " +
            $"needsReplay={report.StrategiesNeedingReplay} eligible={report.AgentsEligibleForReview} " +
            $"paper={report.PaperModified} orders={report.OrderPlacementEnabled} " +
            $"promotion={report.PromotionAllowed}");
        Console.WriteLine("FROZEN_REPORT=" + output);
        return 0;
    }
}
