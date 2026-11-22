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

    /// <summary>
    /// Exploratory comparison, not a PAPER replay: the historical toy model is
    /// all-in USDT, while PAPER sizes positions with BRL risk controls.
    /// Reads only persisted public candles and the existing virtual ledger.
    /// </summary>
    public async Task<object> ValidationAsync(CancellationToken ct)
    {
        var observed = DateTimeOffset.UtcNow;
        await using var db = await factory.CreateDbContextAsync(ct);
        var paper = await db.PaperFills.AsNoTracking()
            .GroupBy(x => x.Symbol)
            .Select(g => new {
                Symbol = g.Key,
                Buys = g.Count(x => x.Side == "BUY"),
                Sells = g.Count(x => x.Side == "SELL"),
                WinningSells = g.Count(x => x.Side == "SELL" && x.RealizedPnlBrl > 0m),
                RealizedBrl = g.Sum(x => x.RealizedPnlBrl ?? 0m),
                FeesBrl = g.Sum(x => x.FeeBrl)
            }).ToDictionaryAsync(x => x.Symbol, ct);
        var account = await db.PaperAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == 1, ct);
        var openPositions = await db.PaperPositions.AsNoTracking()
            .Select(x => new { x.Symbol, x.Quantity, x.EntryPriceBrl, x.LastPriceBrl,
                x.StopPriceBrl, x.EntryFeeBrl }).ToListAsync(ct);
        var exits = await db.PaperFills.AsNoTracking()
            .Where(x => x.Side == "SELL")
            .GroupBy(x => x.Reason)
            .Select(g => new {
                reason = g.Key, sells = g.Count(),
                wins = g.Count(x => x.RealizedPnlBrl > 0m),
                losses = g.Count(x => x.RealizedPnlBrl < 0m),
                realizedPnlBrl = g.Sum(x => x.RealizedPnlBrl ?? 0m)
            }).ToListAsync(ct);
        var results = new List<object>();
        foreach (var symbol in RacMarketSymbols.Supported)
        {
            ct.ThrowIfCancellationRequested();
            var candles = await db.MarketCandles.AsNoTracking()
                .Where(x => x.Symbol == symbol && x.Interval == "1m" && x.CloseUtc <= observed)
                .OrderByDescending(x => x.OpenUtc).Take(1000)
                .Select(x => new MarketCandlePoint(x.Symbol, x.OpenUtc, x.CloseUtc,
                    x.Open, x.High, x.Low, x.Close, x.Volume, x.QuoteVolume, x.Trades))
                .ToListAsync(ct);
            candles.Reverse();
            var contiguous = RacStrategyEvaluator.LatestContiguous(candles);
            var result = RacBacktest.Run(symbol, contiguous, observed);
            paper.TryGetValue(symbol, out var p);
            results.Add(new {
                symbol,
                backtest = result,
                paper = new {
                    buys = p?.Buys ?? 0, sells = p?.Sells ?? 0,
                    winningSells = p?.WinningSells ?? 0,
                    winRatePct = PaperPerformanceMath.WinRatePercent(p?.WinningSells ?? 0, p?.Sells ?? 0),
                    realizedPnlBrl = p?.RealizedBrl ?? 0m,
                    feesBrl = p?.FeesBrl ?? 0m
                },
                verdict = result.Status != "COMPLETE" ? "INSUFFICIENT_DATA" :
                    result.Trades == 0 ? "NO_CLOSED_TRADES" : "EXPLORATORY_ONLY",
                // Different currencies, capital, entry sizing and execution timing:
                // comparing nominal profit as if it were an equivalent strategy is invalid.
                comparablePnl = false
            });
        }
        return new {
            mode = "READ_ONLY_VALIDATION", source = RacMarketSymbols.Source,
            strategyVersion = RacStrategyEvaluator.Version,
            observedUtc = observed, orderPlacementEnabled = false,
            simulationModified = false, count = results.Count,
            riskReview = new {
                running = account?.Running ?? false,
                riskLocked = account?.RiskLocked ?? false,
                riskReason = account?.RiskReason,
                equityBrl = account?.LastEquityBrl ?? 0m,
                dayStartEquityBrl = account?.DailyStartEquityBrl ?? 0m,
                dailyLossPct = account is null ? null : PaperRiskDiagnostics.DailyLossPercent(
                    account.LastEquityBrl, account.DailyStartEquityBrl),
                dailyLossLimitPct = PaperRisk.DailyLossLimitPct,
                financialLimitsChanged = false,
                totalClosedSells = exits.Sum(x => x.sells),
                totalLosingSells = exits.Sum(x => x.losses),
                closedSellLossRatePct = PaperRiskDiagnostics.LossRatePercent(
                    exits.Sum(x => x.losses), exits.Sum(x => x.sells)),
                exitReasons = exits.OrderBy(x => x.reason),
                openPositions = openPositions.Select(x => new {
                    x.Symbol, x.Quantity, x.StopPriceBrl,
                    x.EntryPriceBrl, x.LastPriceBrl,
                    unrealizedPnlBeforeExitFeesBrl = x.Quantity *
                        (x.LastPriceBrl - x.EntryPriceBrl) - x.EntryFeeBrl
                }),
                interpretation = "Risk state is read-only. SELL realized PnL already accounts for entry and exit fees; fee totals must not be subtracted twice. Open positions remain unrealized. No automatic unlock."
            },
            assumptions = new {
                initialCapitalPerAgentUsdt = 100m,
                feePctPerSide = 0.1m, slippagePctPerSide = 0.05m,
                halfSpreadPctPerSide = 0.01m,
                spreadModeledSeparately = true,
                execution = "NEXT_CANDLE_OPEN",
                scope = "IN_SAMPLE_ILLUSTRATIVE",
                limitations = "Backtest all-in USDT is not a replay of PAPER BRL risk sizing, stops, contributions, liquidity or order filters. No out-of-sample validation."
            },
            agents = results
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
