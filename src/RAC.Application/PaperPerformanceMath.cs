namespace RAC.Application;

/// <summary>Read-only metrics over virtual accounting; no strategy or order decisions.</summary>
public static class PaperPerformanceMath
{
    /// <summary>Profitable SELL fills divided by all SELL fills, including break-even sells.</summary>
    public static decimal WinRatePercent(int profitableSells, int allSells) =>
        allSells <= 0 ? 0m : 100m * Math.Clamp(profitableSells, 0, allSells) / allSells;

    /// <summary>Share of current equity occupied by a marked-to-market virtual position.</summary>
    public static decimal ExposurePercent(decimal positionValueBrl, decimal equityBrl) =>
        equityBrl <= 0m ? 0m : Math.Max(0m, positionValueBrl) / equityBrl * 100m;

    /// <summary>Net account result excluding virtual contributions, not an annualized return.</summary>
    public static decimal NetResultBrl(decimal equityBrl, decimal totalContributionsBrl) =>
        equityBrl - totalContributionsBrl;
}
