using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Api.Services;

/// <summary>
/// Authenticated on-demand public-candle research. No DB context, account,
/// Binance credentials or order executor is injected; impossible to mutate PAPER.
/// </summary>
public sealed class RacWalkForwardService(BinancePublicMarketClient publicMarket)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _lastCompleted = DateTimeOffset.MinValue;
    private object? _cached;

    public async Task<object> AnalyzeAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var now = DateTimeOffset.UtcNow;
            // Avoid repeated 12-request public API bursts from repeated clicks.
            if (_cached is not null && now - _lastCompleted < TimeSpan.FromSeconds(60))
                return _cached;
            var results = new List<PaperStrategyWalkForward.Result>();
            var profitabilityDiagnostics = new List<PaperProfitabilityDiagnostics.Result>();
            var entryFilterResearch = new List<PaperEntryFilterResearch.Result>();
            foreach (var symbol in RacMarketSymbols.Supported)
            {
                ct.ThrowIfCancellationRequested();
                // One public request per supported pair, Binance limit=1000;
                // the existing 60-second incremental collector is untouched.
                var candles = await publicMarket.GetHistoricalClosedOneMinuteCandlesAsync(symbol, ct);
                var sorted = candles.OrderBy(x => x.OpenUtc).ToArray();
                var assessedUtc = DateTimeOffset.UtcNow;
                var walk = PaperStrategyWalkForward.Run(symbol, sorted, assessedUtc);
                results.Add(walk);
                profitabilityDiagnostics.Add(PaperProfitabilityDiagnostics.Run(
                    symbol, sorted, assessedUtc, walk));
                entryFilterResearch.Add(PaperEntryFilterResearch.Run(
                    symbol, sorted, assessedUtc, walk));
            }
            if (results.Count != RacMarketSymbols.Supported.Length)
                throw new InvalidOperationException("Incomplete historical response.");
            var payload = new {
                mode = "WALK_FORWARD_RESEARCH_ONLY",
                source = RacMarketSymbols.Source,
                observedUtc = DateTimeOffset.UtcNow,
                count = results.Count,
                historicalMaxCandlesPerPair = PaperStrategyWalkForward.MaxCandles,
                trainingCandles = PaperStrategyWalkForward.TrainCandles,
                testCandles = PaperStrategyWalkForward.TestCandles,
                dataSource = "BINANCE_SPOT_PUBLIC_KLINES_ON_DEMAND",
                historyPersisted = false,
                paperModified = false,
                orderPlacementEnabled = false,
                promotionAllowed = false,
                riskLimitsModified = false,
                assumptions = new {
                    initialUsdtPerWindow = 100m,
                    feePctPerSide = 0.1m,
                    slippagePctPerSide = 0.05m,
                    halfSpreadPctPerSide = 0.01m,
                    method = "ROLLING_TRAIN_ONLY_SELECTION_DISJOINT_HOLDOUTS",
                    baseline = "NO_REENTRY_COOLDOWN",
                    candidate = "FIVE_CANDLE_REENTRY_COOLDOWN",
                    warning = "Limited recent minute history, overlapping training windows, fixed costs and all-in USDT position sizing. NOT a PAPER replay, independent validation or proof of future profit."
                },
                agents = results,
                profitabilityDiagnostics = new {
                    mode = "READ_ONLY_SIGNAL_COST_DIAGNOSTICS",
                    methodology = "CLOSED_CANDLE_SIGNAL_NEXT_CANDLE_RESPONSE_NOT_REALIZED_TRADES",
                    roundTripAssumedCostPct = Math.Round(
                        PaperProfitabilityDiagnostics.RoundTripCostPct, 4),
                    paperModified = false, orderPlacementEnabled = false,
                    promotionAllowed = false, count = profitabilityDiagnostics.Count,
                    agents = profitabilityDiagnostics
                },
                entryFilterResearch = new {
                    mode = "ENTRY_FILTER_WALK_FORWARD_SHADOW_ONLY",
                    method = "TRAIN_ONLY_FILTER_SELECTION_NONOVERLAPPING_HOLDOUTS",
                    count = entryFilterResearch.Count,
                    costAssumptionRoundTripPct = Math.Round(
                        PaperProfitabilityDiagnostics.RoundTripCostPct, 4),
                    paperModified = false, historyPersisted = false,
                    orderPlacementEnabled = false, promotionAllowed = false,
                    riskLimitsModified = false,
                    warning = "Signals per candle are NOT trades. One-minute next-candle response is not real or PAPER PnL; small samples, overlapping training windows and hypothetical costs.",
                    agents = entryFilterResearch
                }
            };
            _cached = payload;
            _lastCompleted = DateTimeOffset.UtcNow;
            return payload;
        }
        finally { _gate.Release(); }
    }
}
