using RAC.Domain;

namespace RAC.Application;

/// <summary>
/// Read-only complete-trade SHADOW simulation. A closed candle decides; buy at
/// the following open, check intrabar stop conservatively, exit at a configured
/// holding horizon. No data persistence, exchange clients or order executors.
/// </summary>
public static class PaperTradeHorizonResearch
{
    public static readonly int[] Horizons = [1, 5, 15, 30];
    private const decimal InitialCash = 100m;
    private const decimal Fee = PaperProfitabilityDiagnostics.FeePctPerSide / 100m;
    private const decimal Impact = (PaperProfitabilityDiagnostics.SlippagePctPerSide +
        PaperProfitabilityDiagnostics.HalfSpreadPctPerSide) / 100m;

    public sealed record Slice(string Status, int Candles, int HorizonMinutes,
        decimal ReturnPct, decimal MaxDrawdownPct, int CompletedTrades,
        int WinningTrades, int LosingTrades, int AtrStopExits,
        int HorizonExits, int FinalLiquidations, decimal? MeanHoldMinutes);
    public sealed record Fold(int Number, int SelectedHorizonFromTraining,
        decimal TrainingSelectedReturnPct, int TrainingSelectedTrades,
        decimal HoldoutBaselineReturnPct, decimal HoldoutSelectedReturnPct,
        int HoldoutSelectedTrades, int HoldoutSelectedStops,
        decimal HoldoutSelectedMaxDrawdownPct, int HoldoutSelectedWins,
        int HoldoutSelectedLosses, decimal? HoldoutMeanHoldMinutes);
    public sealed record Result(string Symbol, string Status, int CandleCount,
        int FoldCount, int SelectedNonBaselineFolds,
        int PositiveSelectedHoldouts, int CompletedSelectedTrades,
        int SelectedStops, decimal MeanBaselineHoldoutReturnPct,
        decimal MeanSelectedHoldoutReturnPct,
        decimal MaxSelectedHoldoutDrawdownPct,
        bool PaperModified, bool OrderPlacementEnabled, bool PromotionAllowed,
        IReadOnlyList<Fold> Folds, string Interpretation);

    public static Result Run(string symbol, IReadOnlyList<MarketCandlePoint> input,
        DateTimeOffset observedUtc, PaperStrategyWalkForward.Result walkForward)
    {
        if (!RacMarketSymbols.IsSupported(symbol))
            throw new ArgumentException("Unsupported market symbol.", nameof(symbol));
        if (input.Count > PaperStrategyWalkForward.MaxCandles ||
            walkForward.Status != "COMPLETE" || walkForward.Symbol != symbol)
            return Blocked(symbol, input.Count);
        var data = RacStrategyEvaluator.LatestContiguous(input);
        if (data.Length < PaperStrategyWalkForward.TrainCandles +
                PaperStrategyWalkForward.TestCandles * PaperStrategyWalkForward.MinimumFolds ||
            RacStrategyEvaluator.Evaluate(symbol, data, observedUtc).Status != "READY")
            return Blocked(symbol, data.Length);
        var folds = new List<Fold>();
        for (int start = PaperStrategyWalkForward.TrainCandles;
             start + PaperStrategyWalkForward.TestCandles <= data.Length;
             start += PaperStrategyWalkForward.TestCandles)
        {
            var train = data.Skip(start - PaperStrategyWalkForward.TrainCandles)
                .Take(PaperStrategyWalkForward.TrainCandles).ToArray();
            var test = data.Skip(start).Take(PaperStrategyWalkForward.TestCandles).ToArray();
            var trainCandidates = Horizons.Select(h => SimulateSlice(symbol, train, h)).ToArray();
            if (trainCandidates.Any(x => x.Status != "COMPLETE"))
                return Blocked(symbol, data.Length);
            // Select only from training; a candidate must have at least one
            // completed simulated round trip. Ties keep the shorter horizon.
            var winner = trainCandidates[0];
            foreach (var candidate in trainCandidates.Skip(1))
                if (candidate.CompletedTrades > 0 &&
                    candidate.ReturnPct > winner.ReturnPct)
                    winner = candidate;
            var holdBase = SimulateSlice(symbol, test, 1);
            var holdSelected = winner.HorizonMinutes == 1
                ? holdBase : SimulateSlice(symbol, test, winner.HorizonMinutes);
            if (holdBase.Status != "COMPLETE" || holdSelected.Status != "COMPLETE")
                return Blocked(symbol, data.Length);
            folds.Add(new Fold(folds.Count + 1, winner.HorizonMinutes,
                winner.ReturnPct, winner.CompletedTrades,
                holdBase.ReturnPct, holdSelected.ReturnPct,
                holdSelected.CompletedTrades, holdSelected.AtrStopExits,
                holdSelected.MaxDrawdownPct, holdSelected.WinningTrades,
                holdSelected.LosingTrades, holdSelected.MeanHoldMinutes));
        }
        if (folds.Count != walkForward.FoldCount)
            return Blocked(symbol, data.Length);
        return new(symbol, "COMPLETE", data.Length, folds.Count,
            folds.Count(x => x.SelectedHorizonFromTraining != 1),
            folds.Count(x => x.HoldoutSelectedReturnPct > 0m),
            folds.Sum(x => x.HoldoutSelectedTrades),
            folds.Sum(x => x.HoldoutSelectedStops),
            Math.Round(folds.Average(x => x.HoldoutBaselineReturnPct), 4),
            Math.Round(folds.Average(x => x.HoldoutSelectedReturnPct), 4),
            folds.Max(x => x.HoldoutSelectedMaxDrawdownPct),
            false, false, false, folds,
            "Self-contained 100 USDT SHADOW trades. Training-only horizon selection, then disjoint later holdouts. Closed-candle signal, next-open entry, stop 2x ATR, holding-time or end-window exits, fixed costs. Holding duration measured in candle minutes. Training windows overlap. No live/PAPER executions or strategy promotion.");
    }

    public static Slice SimulateSlice(string symbol, IReadOnlyList<MarketCandlePoint> input,
        int horizonMinutes, decimal costMultiplier = 1m, string entryGate = "BASELINE")
    {
        if (!RacMarketSymbols.IsSupported(symbol))
            throw new ArgumentException("Unsupported market symbol.", nameof(symbol));
        if (!Horizons.Contains(horizonMinutes))
            throw new ArgumentOutOfRangeException(nameof(horizonMinutes));
        if (costMultiplier is not (1m or 2m))
            throw new ArgumentOutOfRangeException(nameof(costMultiplier));
        if (entryGate is not ("BASELINE" or "TREND_STRENGTH" or "ATR_COST" or
            "TREND_AND_ATR" or "NO_TRADE"))
            throw new ArgumentOutOfRangeException(nameof(entryGate));
        var fee = Fee * costMultiplier;
        var impact = Impact * costMultiplier;
        if (input.Count < RacStrategyEvaluator.MinimumCandles + 10 || input.Count > 2000 ||
            input.Any(x => x.Symbol != symbol || x.Open <= 0m || x.Close <= 0m ||
                x.High < x.Low || x.Low <= 0m ||
                x.Open < x.Low || x.Open > x.High ||
                x.Close < x.Low || x.Close > x.High ||
                x.Volume < 0m || x.QuoteVolume < 0m || x.Trades < 0 ||
                x.CloseUtc <= x.OpenUtc ||
                x.CloseUtc > x.OpenUtc.AddMinutes(1)))
            return Unavailable(input.Count, horizonMinutes);
        for (var j = 1; j < input.Count; j++)
            if (input[j].OpenUtc - input[j - 1].OpenUtc != TimeSpan.FromMinutes(1) ||
                input[j - 1].CloseUtc >= input[j].OpenUtc)
                return Unavailable(input.Count, horizonMinutes);
        var candles = input.ToArray();
        if (entryGate == "NO_TRADE")
            return new("COMPLETE", candles.Length, horizonMinutes, 0m, 0m,
                0, 0, 0, 0, 0, 0, null);
        decimal cash = InitialCash, qty = 0m, stop = 0m, entryCapital = 0m;
        decimal peak = InitialCash, maxDd = 0m;
        int enteredAt = -1, completed = 0, wins = 0, losses = 0;
        int stops = 0, timeExits = 0, liquidations = 0, sumHold = 0;
        for (var i = RacStrategyEvaluator.MinimumCandles - 1; i < candles.Length - 1; i++)
        {
            var nextIndex = i + 1;
            var next = candles[nextIndex];
            // A position held through the previous close is sold at the next
            // open if its holding period is already complete. No same-bar fill.
            if (qty > 0m && nextIndex - enteredAt >= horizonMinutes)
            {
                Exit(next.Open, nextIndex, ref timeExits);
            }
            if (qty == 0m)
            {
                var signal = RacStrategyEvaluator.Evaluate(symbol,
                    candles.Take(i + 1).ToArray(), candles[i].CloseUtc);
                if (signal.Status == "READY" && signal.Action == "COMPRA_OBSERVACIONAL" &&
                    signal.Atr14 is > 0m && signal.Sma9 is not null &&
                    signal.Sma21 is not null &&
                    EntryGatePasses(signal, candles[i].Close, entryGate, costMultiplier))
                {
                    entryCapital = cash;
                    var buy = PaperExecutionAudit.BuyAll(cash, next.Open, costMultiplier);
                    var entry = buy.ImpactAdjustedEntryQuote;
                    qty = buy.FilledQuantity;
                    cash = 0m;
                    enteredAt = nextIndex;
                    stop = Math.Max(0m, entry - 2m * signal.Atr14.Value);
                }
            }
            if (qty > 0m && next.Low <= stop)
            {
                // A gap below the stop exits at the worse opening quote.
                Exit(PaperExecutionAudit.ConservativeStopFill(stop, next.Open,
                    next.Low), nextIndex, ref stops);
            }
            else if (qty > 0m && nextIndex - enteredAt + 1 >= horizonMinutes)
            {
                // The current closed candle reaches the holding horizon.
                Exit(next.Close, nextIndex + 1, ref timeExits);
            }
            var equity = cash + qty * next.Close * (1m - impact) * (1m - fee);
            peak = Math.Max(peak, equity);
            maxDd = Math.Max(maxDd, (peak - equity) / peak * 100m);
        }
        if (qty > 0m) Exit(candles[^1].Close, candles.Length, ref liquidations);
        peak = Math.Max(peak, cash);
        maxDd = Math.Max(maxDd, (peak - cash) / peak * 100m);
        return new("COMPLETE", candles.Length, horizonMinutes,
            Math.Round((cash / InitialCash - 1m) * 100m, 4),
            Math.Round(maxDd, 4), completed, wins, losses, stops,
            timeExits, liquidations,
            completed > 0 ? Math.Round((decimal)sumHold / completed, 2) : null);

        void Exit(decimal quotedPrice, int exclusiveExitCandleIndex, ref int reasonCounter)
        {
            cash = PaperExecutionAudit.SellAll(qty, quotedPrice,
                costMultiplier).NetCashAfter;
            qty = 0m;
            completed++;
            if (cash > entryCapital) wins++;
            else if (cash < entryCapital) losses++;
            sumHold += Math.Max(1, exclusiveExitCandleIndex - enteredAt);
            reasonCounter++;
            enteredAt = -1;
        }
    }

    public static bool EntryGatePasses(StrategySignal signal, decimal closedPrice,
        string entryGate, decimal costMultiplier)
    {
        if (closedPrice <= 0m || signal.Sma9 is null || signal.Sma21 is null ||
            signal.Atr14 is null || signal.Status != "READY") return false;
        // Predeclared thresholds; only known closed-candle indicators are used.
        var smaSeparation = (signal.Sma9.Value - signal.Sma21.Value) / closedPrice * 100m;
        var atrPct = signal.Atr14.Value / closedPrice * 100m;
        var trend = smaSeparation >= 0.05m;
        var volatileEnough = atrPct >=
            PaperProfitabilityDiagnostics.RoundTripCostPct * costMultiplier / 2m;
        return entryGate switch
        {
            "BASELINE" => true,
            "TREND_STRENGTH" => trend,
            "ATR_COST" => volatileEnough,
            "TREND_AND_ATR" => trend && volatileEnough,
            "NO_TRADE" => false,
            _ => throw new ArgumentOutOfRangeException(nameof(entryGate))
        };
    }

    private static Slice Unavailable(int n, int horizon) =>
        new("BLOCKED", n, horizon, 0m, 0m, 0, 0, 0, 0, 0, 0, null);
    private static Result Blocked(string symbol, int n) =>
        new(symbol, "INSUFFICIENT_DATA", n, 0, 0, 0, 0, 0,
            0m, 0m, 0m, false, false, false, [],
            "Requires complete recent contiguous data and valid walk-forward test periods.");
}
