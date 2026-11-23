using RAC.Domain;

namespace RAC.Application;

/// <summary>Offline SHADOW exit-capacity replay of a hypothetical inventory.
/// Volume is a capacity proxy, not exchange order-book depth or actual fills.
/// No account, SQL, exchange client or orders are accessed.</summary>
public static class PaperExitLiquidityAudit
{
    public sealed record Result(string Status, int CandlesSeen, int SellAttempts,
        int PartialSellAttempts, int NoFillAttempts, decimal InitialQuantity,
        decimal QuantitySold, decimal QuantityUnresolved, decimal NetCashRealized,
        decimal ResidualMarkToMarketUsdt, decimal RealizedFraction,
        decimal EstimatedTotalEquityUsdt, bool AllSold, bool PaperModified,
        bool OrderPlacementEnabled, bool PromotionAllowed);

    public static Result Replay(decimal quantity, IReadOnlyList<MarketCandlePoint> candles,
        decimal maxParticipation, decimal costMultiplier, decimal stopPrice = 0m)
    {
        if (quantity <= 0m || candles.Count is < 1 or > 60 ||
            maxParticipation is not (0.001m or 0.01m) ||
            costMultiplier is not (1m or 2m) || stopPrice < 0m)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        for (var i = 0; i < candles.Count; i++)
        {
            var c = candles[i];
            if (c.Open <= 0m || c.Low <= 0m || c.High < c.Open ||
                c.Low > c.Open || c.Close < c.Low || c.Close > c.High ||
                c.Volume < 0m || c.QuoteVolume < 0m || c.Trades < 0 ||
                c.CloseUtc <= c.OpenUtc || c.CloseUtc >= c.OpenUtc.AddMinutes(1) ||
                (i > 0 && c.OpenUtc - candles[i - 1].OpenUtc != TimeSpan.FromMinutes(1)))
                throw new InvalidDataException("Invalid or non-contiguous exit candle.");
        }
        var original = quantity;
        decimal netCash = 0m;
        int partial = 0, blocked = 0, attempts = 0, seen = 0;
        foreach (var c in candles)
        {
            if (quantity <= 0m) break;
            attempts++; seen++;
            // A gap below the stop is priced at the worse opening quote;
            // otherwise the stop quote is used if candle low reaches it.
            var price = stopPrice > 0m && c.Low <= stopPrice
                ? Math.Min(stopPrice, c.Open) : c.Open;
            var capacity = Math.Min(c.Volume,
                c.QuoteVolume / c.Open) * maxParticipation;
            var sold = Math.Min(quantity, capacity);
            if (sold <= 0m) { blocked++; continue; }
            if (sold < quantity) partial++;
            // Intrabar range penalty is a deliberately hypothetical stress.
            var rangeImpact = Math.Min(0.02m,
                (c.High - c.Low) / c.Open * maxParticipation * 10m);
            var effectivePrice = price * (1m - rangeImpact);
            netCash += PaperExecutionAudit.SellAll(sold, effectivePrice,
                costMultiplier).NetCashAfter;
            quantity -= sold;
        }
        // Unfilled quantity is not sold. Mark it separately to the last close
        // with a conservative haircut for risk visibility, not realized PnL.
        var haircut = 0.10m;
        var residualValue = quantity * candles[^1].Close * (1m - haircut);
        return new(quantity == 0m ? "FULLY_SIMULATED_EXIT" : "UNRESOLVED_EXIT",
            seen, attempts, partial, blocked, original, original - quantity,
            quantity, Math.Round(netCash, 8), Math.Round(residualValue, 8),
            Math.Round((original - quantity) / original, 8),
            Math.Round(netCash + residualValue, 8), quantity == 0m,
            false, false, false);
    }
}
