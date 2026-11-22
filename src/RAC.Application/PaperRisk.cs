namespace RAC.Application;

/// <summary>Fixed fail-closed limits for virtual BRL-only trading.</summary>
public static class PaperRisk
{
    public const decimal StartingCapitalBrl = 100m;
    public const decimal MonthlyVirtualContributionBrl = 200m;
    public const decimal MaxRiskPerTradePct = 0.5m;
    public const decimal DailyLossLimitPct = 1m;
    public const decimal MaxDrawdownPct = 10m;
    public const decimal MaxPositionPct = 20m;
    public const decimal MaxTotalExposurePct = 30m;
    public const int MaxOpenPositions = 1;
    public const int MaxEntriesPerDay = 0; // 0 = sem limite por contagem diária; limites financeiros permanecem.
    public const decimal FeePercentPerSide = 0.1m;
    public const decimal SlippagePercentPerSide = 0.05m;
    public const decimal ReinvestmentPercent = 100m;

    public static string? LockReason(decimal equity, decimal dayStart, decimal peak)
    {
        if (equity < 0m || dayStart <= 0m || peak <= 0m)
            return "Patrimônio ou referência de risco inválida.";
        if ((dayStart - equity) / dayStart * 100m >= DailyLossLimitPct)
            return "Carlos: perda diária máxima de 1% atingida.";
        if ((peak - equity) / peak * 100m >= MaxDrawdownPct)
            return "Carlos: queda acumulada máxima de 10% atingida.";
        return null;
    }

    /// <summary>
    /// Position size constrained by a 2 ATR stop, 0.5% risk, max 20% per
    /// position and 30% total exposure. Always includes entry fee in cash.
    /// </summary>
    public static decimal Quantity(decimal cashBrl, decimal equityBrl,
        decimal priceBrl, decimal atrBrl, decimal currentExposureBrl,
        int openPositions, int entriesToday)
    {
        if (cashBrl <= 0 || equityBrl <= 0 || priceBrl <= 0 || atrBrl <= 0 ||
            currentExposureBrl < 0 || openPositions >= MaxOpenPositions)
            return 0m;
        var stopDistance = atrBrl * 2m;
        if (stopDistance <= 0m) return 0m;
        var riskBudget = equityBrl * MaxRiskPerTradePct / 100m;
        var grossLimit = Math.Min(equityBrl * MaxPositionPct / 100m,
            Math.Min(equityBrl * MaxTotalExposurePct / 100m - currentExposureBrl,
                cashBrl / (1m + FeePercentPerSide / 100m)));
        if (grossLimit <= 0m) return 0m;
        // Reserve both fees and a two-sided slippage allowance within 0.5% risk.
        var roundTripCosts = priceBrl * (2m * FeePercentPerSide +
            2m * SlippagePercentPerSide) / 100m;
        var quantity = Math.Min(riskBudget / (stopDistance + roundTripCosts),
            grossLimit / priceBrl);
        // Avoid decimal overflow and dust; a paper fill always needs a positive notional.
        if (quantity <= 0m || quantity * priceBrl < 1m) return 0m;
        return decimal.Round(quantity, 12, MidpointRounding.ToZero);
    }
}
