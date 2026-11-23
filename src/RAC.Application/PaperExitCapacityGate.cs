using RAC.Domain;

namespace RAC.Application;

/// <summary>
/// Post-hoc SHADOW exit feasibility gate for trade events recorded by a
/// completed holdout. Never replaces the simulator's hypothetical PnL with
/// sale proceeds or represents actual exchange fills. No order capability.
/// </summary>
public static class PaperExitCapacityGate
{
    public sealed record Result(string Status, int IntendedExits, int CoveredExits,
        int UnresolvedExits, int MissingFutureCandles,
        decimal TotalUnresolvedQuantity, bool ResearchReviewEligible,
        bool PaperModified, bool OrderPlacementEnabled, bool PromotionAllowed);

    public static Result Assess(PaperTradeHorizonResearch.Slice slice,
        IReadOnlyList<MarketCandlePoint> holdout, decimal costMultiplier = 1m)
    {
        if (costMultiplier is not (1m or 2m))
            throw new ArgumentOutOfRangeException(nameof(costMultiplier));
        var intents = slice.ExitIntents ??
            (slice.CompletedTrades == 0 ? [] :
                throw new InvalidDataException("Missing trade exit evidence."));
        if (slice.Status != "COMPLETE" ||
            slice.Candles != holdout.Count ||
            slice.CompletedTrades != intents.Count ||
            intents.Any(x => x.RequestedQuantity <= 0m ||
                x.NextCandleIndex < 0 || x.NextCandleIndex > holdout.Count))
            throw new InvalidDataException("Exit data missing or inconsistent.");
        var covered = 0; var missing = 0; var unresolved = 0;
        decimal remainingQuantity = 0m;
        foreach (var intent in intents)
        {
            var available = holdout.Skip(intent.NextCandleIndex).Take(5).ToArray();
            if (available.Length < 5)
            {
                missing++; unresolved++;
                remainingQuantity += intent.RequestedQuantity;
                continue;
            }
            var assessed = PaperExitLiquidityAudit.Replay(intent.RequestedQuantity,
                available, 0.001m, costMultiplier);
            if (assessed.AllSold) covered++;
            else
            {
                unresolved++;
                remainingQuantity += assessed.QuantityUnresolved;
            }
        }
        return new Result(slice.CompletedTrades == 0 ? "NO_TRADES" :
            unresolved > 0 ? "EXIT_UNRESOLVED_REJECTED" :
            "EXIT_COVERED_SHADOW_ONLY", slice.CompletedTrades, covered,
            unresolved, missing, Math.Round(remainingQuantity, 8),
            slice.CompletedTrades > 0 && unresolved == 0,
            false, false, false);
    }
}
