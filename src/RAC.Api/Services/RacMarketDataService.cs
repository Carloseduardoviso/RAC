using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Api.Services;

public sealed record MarketFeedHealth(string Status, string Source,
    DateTimeOffset? LastAttemptUtc, DateTimeOffset? LastSuccessUtc,
    DateTimeOffset? NextAttemptUtc, string? Error);

/// <summary>
/// Independent market-data ingestion. Never touches simulated trading positions.
/// </summary>
public sealed class RacMarketDataService(
    BinancePublicMarketClient binance,
    IDbContextFactory<RacDbContext> factory,
    ILogger<RacMarketDataService> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _healthGate = new();
    private DateTimeOffset? _lastAttempt;
    private DateTimeOffset? _lastSuccess;
    private DateTimeOffset? _nextAttempt;
    private DateOnly? _lastCleanup;
    private string? _lastError;

    public MarketFeedHealth Health
    {
        get
        {
            lock (_healthGate)
            {
                string status;
                if (_lastSuccess is null) status = "WAITING";
                else if (_lastSuccess < DateTimeOffset.UtcNow.AddMinutes(-3)) status = "STALE";
                else status = _lastError is null ? "LIVE" : "DEGRADED";
                return new MarketFeedHealth(status, RacMarketSymbols.Source,
                    _lastAttempt, _lastSuccess, _nextAttempt, _lastError);
            }
        }
    }

    public async Task RefreshAsync(CancellationToken ct)
    {
        if (_nextAttempt > DateTimeOffset.UtcNow) return;
        await _gate.WaitAsync(ct);
        try
        {
            if (_nextAttempt > DateTimeOffset.UtcNow) return;
            lock (_healthGate) _lastAttempt = DateTimeOffset.UtcNow;

            // One public batched request for the 12 pairs, then bounded 1m-candle requests.
            var quotes = await binance.GetPricesAsync(ct);
            if (quotes.Count != RacMarketSymbols.Supported.Length)
                throw new FormatException("A Binance não retornou os 12 pares esperados.");

            var observedUtc = DateTimeOffset.UtcNow;
            await using var db = await factory.CreateDbContextAsync(ct);
            var existingQuotes = await db.MarketQuotes.ToDictionaryAsync(x => x.Symbol, ct);
            foreach (var quote in quotes)
            {
                if (!existingQuotes.TryGetValue(quote.Symbol, out var saved))
                {
                    saved = new MarketQuote { Symbol = quote.Symbol };
                    db.MarketQuotes.Add(saved);
                }
                saved.PriceUsdt = quote.PriceUsdt;
                saved.ObservedUtc = observedUtc;
                saved.Source = RacMarketSymbols.Source;
            }
            await db.SaveChangesAsync(ct);
            lock (_healthGate)
            {
                _lastSuccess = observedUtc;
                _lastError = null;
            }

            var failedSymbols = new List<string>();
            foreach (var symbol in RacMarketSymbols.Supported)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var recent = await db.MarketCandles.AsNoTracking()
                        .Where(x => x.Symbol == symbol && x.Interval == RacMarketSymbols.CandleInterval)
                        .OrderByDescending(x => x.OpenUtc).Take(120)
                        .Select(x => x.OpenUtc).ToListAsync(ct);
                    var candles = await binance.GetClosedOneMinuteCandlesAsync(symbol,
                        recent.Count == 0 ? 90 : 4, ct);
                    var known = recent.ToHashSet();
                    foreach (var candle in candles)
                    {
                        if (!known.Add(candle.OpenUtc)) continue;
                        db.MarketCandles.Add(new MarketCandle
                        {
                            Symbol = symbol, OpenUtc = candle.OpenUtc, CloseUtc = candle.CloseUtc,
                            Open = candle.Open, High = candle.High, Low = candle.Low,
                            Close = candle.Close, Volume = candle.Volume,
                            QuoteVolume = candle.QuoteVolume, Trades = candle.Trades
                        });
                    }
                    await db.SaveChangesAsync(ct);
                }
                catch (BinanceRateLimitException)
                {
                    // Stop making requests immediately on a 429/418 to protect this IP.
                    throw;
                }
                catch (HttpRequestException ex)
                {
                    failedSymbols.Add(symbol);
                    logger.LogWarning(ex, "Falha de rede Binance para {Symbol}", symbol);
                }
                catch (FormatException ex)
                {
                    failedSymbols.Add(symbol);
                    logger.LogWarning(ex, "Candle inválido para {Symbol}", symbol);
                }
            }

            // Retain up to 14 days to prevent silent unbounded SQL Server growth.
            var date = DateOnly.FromDateTime(DateTime.UtcNow);
            if (_lastCleanup != date)
            {
                var cutoff = DateTimeOffset.UtcNow.AddDays(-14);
                await db.MarketCandles.Where(x => x.CloseUtc < cutoff).ExecuteDeleteAsync(ct);
                _lastCleanup = date;
            }
            if (failedSymbols.Count != 0)
                lock (_healthGate) _lastError = $"Candles indisponíveis: {string.Join(", ", failedSymbols)}";
        }
        catch (BinanceRateLimitException ex)
        {
            logger.LogWarning("Limite Binance atingido: suspensas consultas por {Delay}.", ex.RetryAfter);
            lock (_healthGate)
            {
                _nextAttempt = DateTimeOffset.UtcNow.Add(ex.RetryAfter);
                _lastError = "Limite de requisições da Binance; aguardando liberação.";
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Coleta de cotações da Binance falhou.");
            lock (_healthGate) _lastError = "Falha de rede, resposta ou persistência dos dados públicos.";
        }
        finally { _gate.Release(); }
    }
}

public sealed class RacMarketDataWorker(
    RacMarketDataService collector,
    IConfiguration configuration,
    ILogger<RacMarketDataWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Rac:MarketData:Enabled", true))
        {
            logger.LogInformation("Coleta pública de mercado desativada pela configuração.");
            return;
        }

        // Bounds avoid accidental request storms, even if misconfigured.
        var seconds = Math.Clamp(configuration.GetValue("Rac:MarketData:RefreshSeconds", 60), 60, 600);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        try
        {
            await collector.RefreshAsync(stoppingToken);
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await collector.RefreshAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Coleta Binance encerrada.");
        }
    }
}
