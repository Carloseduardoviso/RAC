namespace RAC.Application;

/// <summary>Read-only risk diagnosis. It never changes risk locks or account balances.</summary>
public static class PaperRiskDiagnostics
{
    /// <summary>Returns null when the reference capital is invalid or absent.</summary>
    public static decimal? DailyLossPercent(decimal equityBrl, decimal dayStartBrl) =>
        dayStartBrl <= 0m ? null :
        Math.Max(0m, (dayStartBrl - equityBrl) / dayStartBrl * 100m);

    /// <summary>Reporting-only loss proportion. Null means there were no completed sells.</summary>
    public static decimal? LossRatePercent(int losses, int sells) =>
        sells <= 0 ? null : Math.Clamp(losses, 0, sells) * 100m / sells;
}
