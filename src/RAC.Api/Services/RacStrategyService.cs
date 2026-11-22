using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Api.Services;

/// <summary>
/// Shadow analysts for all twelve symbols. Reads public persisted closed
/// candles and never calls Binance private endpoints or modifies the simulator.
/// </summary>
public sealed class RacStrategyService(IDbContextFactory<RacDbContext> factory)
{
    public async Task<object> OverviewAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-180);
        var records = await db.MarketCandles.AsNoTracking()
            .Where(x => x.Interval == "1m" && x.CloseUtc >= cutoff)
            .OrderByDescending(x => x.OpenUtc)
            .Take(3000)
            .Select(x => new MarketCandlePoint(x.Symbol, x.OpenUtc, x.CloseUtc,
                x.Open, x.High, x.Low, x.Close, x.Volume, x.QuoteVolume, x.Trades))
            .ToListAsync(ct);

        var bySymbol = records.GroupBy(x => x.Symbol)
            .ToDictionary(x => x.Key, x => x.OrderBy(c => c.OpenUtc)
                .TakeLast(120).ToArray(), StringComparer.Ordinal);
        var now = DateTimeOffset.UtcNow;
        var signals = RacMarketSymbols.Supported.Select(symbol =>
            RacStrategyEvaluator.Evaluate(symbol,
                RacStrategyEvaluator.LatestContiguous(bySymbol.GetValueOrDefault(symbol) ?? []), now)).ToArray();
        return new
        {
            source = RacMarketSymbols.Source, mode = "OBSERVATIONAL_ONLY",
            strategyVersion = RacStrategyEvaluator.Version,
            orderPlacementEnabled = false, simulationLinked = false,
            timestampUtc = now, agents = signals
        };
    }

    public async Task<object> BacktestAsync(string symbol, CancellationToken ct)
    {
        if (!RacMarketSymbols.IsSupported(symbol))
            throw new ArgumentException("Par não monitorado.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var data = await db.MarketCandles.AsNoTracking()
            .Where(x => x.Symbol == symbol && x.Interval == "1m")
            .OrderByDescending(x => x.OpenUtc).Take(1000)
            .Select(x => new MarketCandlePoint(x.Symbol, x.OpenUtc, x.CloseUtc,
                x.Open, x.High, x.Low, x.Close, x.Volume, x.QuoteVolume, x.Trades))
            .ToListAsync(ct);
        data.Reverse();
        return RacBacktest.Run(symbol, data, DateTimeOffset.UtcNow);
    }
}
