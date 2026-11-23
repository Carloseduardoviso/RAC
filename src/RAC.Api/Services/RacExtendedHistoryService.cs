using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Api.Services;

/// <summary>
/// On-demand 48h public research, deliberately isolated from the regular
/// 1000-candle panel request and from all accounts, SQL and order executors.
/// </summary>
public sealed class RacExtendedHistoryService(BinancePublicMarketClient market)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private object? _cached;
    private DateTimeOffset _completed = DateTimeOffset.MinValue;

    public async Task<object> AnalyzeAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_cached is not null && DateTimeOffset.UtcNow - _completed < TimeSpan.FromMinutes(15))
                return _cached;
            var results = new List<PaperExtendedHistoryResearch.Result>();
            foreach (var symbol in RacMarketSymbols.Supported)
            {
                ct.ThrowIfCancellationRequested();
                var history = await market.GetExtendedClosedOneMinuteCandlesAsync(symbol, ct);
                results.Add(PaperExtendedHistoryResearch.Run(symbol, history,
                    DateTimeOffset.UtcNow));
            }
            if (results.Count != RacMarketSymbols.Supported.Length)
                throw new InvalidOperationException("Incomplete 12-symbol research response.");
            var payload = new {
                mode = "EXTENDED_HISTORY_48H_SHADOW_ONLY",
                source = RacMarketSymbols.Source,
                dataSource = "BINANCE_SPOT_PUBLIC_PAGINATED_1M_KLINES",
                observedUtc = DateTimeOffset.UtcNow,
                count = results.Count,
                minutesPerSymbol = PaperExtendedHistoryResearch.HistoryCandles,
                pagesPerSymbolMax = 3,
                trainedCandlesPerFold = PaperExtendedHistoryResearch.TrainCandles,
                testedCandlesPerFold = PaperExtendedHistoryResearch.HoldoutCandles,
                horizonsMinutes = PaperTradeHorizonResearch.Horizons,
                initialUsdtPerFold = 100m,
                comparisonWithNoTradingReturnPct = 0m,
                historyPersisted = false, paperModified = false,
                riskLimitsModified = false, orderPlacementEnabled = false,
                promotionAllowed = false,
                warning = "Limited 48h and assumed costs. Results are exploratory, not proof of future profit. SHADOW only; no orders, no PAPER changes, no auto-promotion.",
                agents = results
            };
            _cached = payload;
            _completed = DateTimeOffset.UtcNow;
            return payload;
        }
        finally { _gate.Release(); }
    }
}
