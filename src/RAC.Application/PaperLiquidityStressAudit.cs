namespace RAC.Application;

/// <summary>
/// SHADOW capacity and execution-cost sensitivity, NOT reconstructed order-book
/// fills. One candle's PUBLIC OHLCV is a research input, not observable depth.
/// Does not place orders, forecast profits or modify the PAPER account.
/// </summary>
public static class PaperLiquidityStressAudit
{
    public sealed record Quote(decimal RequestedNotional, decimal FilledNotional,
        decimal UnfilledNotional, decimal FillFraction, decimal EffectiveBuyPrice,
        decimal EffectiveSellPrice, decimal BuyFee, decimal SellFee,
        decimal EstimatedRoundTripCostPct, decimal ExtraRangeImpactPct,
        bool InsufficientLiquidity, bool PaperModified,
        bool OrderPlacementEnabled, bool PromotionAllowed);

    public static Quote Assess(decimal open, decimal high, decimal low,
        decimal baseVolume, decimal quoteVolume, decimal requestedUsdt,
        decimal maxParticipation, decimal costMultiplier)
    {
        if (open <= 0m || low <= 0m || high < open || low > open ||
            baseVolume < 0m || quoteVolume < 0m || requestedUsdt <= 0m ||
            maxParticipation is not (0.001m or 0.01m) ||
            costMultiplier is not (1m or 2m))
            throw new ArgumentOutOfRangeException(nameof(open));
        // Do not trust aggregate kline volume as executable order-book depth.
        // Cap hypothetical participation; zero-volume candles have no fills.
        var capacity = Math.Min(baseVolume * open, quoteVolume) * maxParticipation;
        var filled = Math.Min(requestedUsdt, capacity);
        var residual = requestedUsdt - filled;
        if (filled <= 0m)
            return new(requestedUsdt, 0m, requestedUsdt, 0m, 0m, 0m,
                0m, 0m, 0m, 0m, true, false, false, false);
        var rate = PaperProfitabilityDiagnostics.FeePctPerSide / 100m * costMultiplier;
        var impact = (PaperProfitabilityDiagnostics.SlippagePctPerSide +
            PaperProfitabilityDiagnostics.HalfSpreadPctPerSide) / 100m * costMultiplier;
        // Stress proxy: bounded fraction of observed intraminute range.
        // This is a hypothetical scenario, not contemporaneous quoted spread.
        var rangeImpact = Math.Min(0.02m, (high - low) / open * maxParticipation * 10m);
        impact += rangeImpact;
        var buyQuote = open * (1m + impact);
        var sellQuote = open * (1m - impact);
        var quantity = filled * (1m - rate) / buyQuote;
        var proceeds = quantity * sellQuote * (1m - rate);
        var buyFee = filled * rate;
        var sellFee = quantity * sellQuote * rate;
        var lossPct = (filled - proceeds) / filled * 100m;
        return new(requestedUsdt, filled, residual, filled / requestedUsdt,
            buyQuote, sellQuote, buyFee, sellFee, lossPct,
            rangeImpact * 100m, residual > 0m, false, false, false);
    }
}
