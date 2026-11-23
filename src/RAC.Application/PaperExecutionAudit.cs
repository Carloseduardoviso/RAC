namespace RAC.Application;

/// <summary>
/// Pure SHADOW price and cost arithmetic. No accounts, broker API, orders or SQL.
/// A modeled quote is a conservative estimate, NOT an actual exchange fill.
/// </summary>
public static class PaperExecutionAudit
{
    public sealed record Entry(decimal CashBefore, decimal MarketOpen,
        decimal ImpactAdjustedEntryQuote, decimal FeePaidInCash,
        decimal FilledQuantity);
    public sealed record Exit(decimal Quantity, decimal MarketQuote,
        decimal ImpactAdjustedExitQuote, decimal FeePaidInCash,
        decimal NetCashAfter);

    public static Entry BuyAll(decimal cash, decimal openQuote, decimal costMultiplier)
    {
        var (fee, impact) = Rates(costMultiplier);
        if (cash <= 0m || openQuote <= 0m)
            throw new ArgumentOutOfRangeException(nameof(openQuote));
        var entryQuote = openQuote * (1m + impact);
        var buyFee = cash * fee;
        return new Entry(cash, openQuote, entryQuote, buyFee,
            (cash - buyFee) / entryQuote);
    }

    public static Exit SellAll(decimal quantity, decimal marketQuote, decimal costMultiplier)
    {
        var (fee, impact) = Rates(costMultiplier);
        if (quantity <= 0m || marketQuote <= 0m)
            throw new ArgumentOutOfRangeException(nameof(marketQuote));
        var exitQuote = marketQuote * (1m - impact);
        var gross = quantity * exitQuote;
        var sellFee = gross * fee;
        return new Exit(quantity, marketQuote, exitQuote, sellFee, gross - sellFee);
    }

    /// <summary>A stop crossed during a 1m candle cannot fill above the stop.
    /// A gap down opens below the stop and is filled at the worse opening price.</summary>
    public static decimal ConservativeStopFill(decimal stop, decimal open,
        decimal low)
    {
        if (stop <= 0m || open <= 0m || low <= 0m || low > open || low > stop)
            throw new ArgumentOutOfRangeException(nameof(low));
        return Math.Min(open, stop);
    }

    private static (decimal Fee, decimal Impact) Rates(decimal multiplier)
    {
        if (multiplier is not (1m or 2m))
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        var fee = PaperProfitabilityDiagnostics.FeePctPerSide / 100m * multiplier;
        var impact = (PaperProfitabilityDiagnostics.SlippagePctPerSide +
            PaperProfitabilityDiagnostics.HalfSpreadPctPerSide) / 100m * multiplier;
        return (fee, impact);
    }
}
