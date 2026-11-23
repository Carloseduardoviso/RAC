using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Api.Services;

/// <summary>
/// A completely separate virtual BRL account driven by public Binance prices
/// and the 12 strategy signals. No use of private Binance credentials.
/// Startup always pauses, and unexpected failures stop automatic execution.
/// </summary>
public sealed class RacPaperTradingService(
    IDbContextFactory<RacDbContext> factory,
    BinancePortfolioPricesClient prices,
    ILogger<RacPaperTradingService> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await using var db = await factory.CreateDbContextAsync(ct);
        var account = await db.PaperAccounts.SingleOrDefaultAsync(x => x.Id == 1, ct);
        if (account is null)
        {
            db.PaperAccounts.Add(new PaperAccount { Id = 1, Running = false });
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Carteira virtual MVP 1.1 criada pausada com R$ 100 fictícios.");
        }
        else if (account.Running)
        {
            account.Running = false; // never resume trading after process restart.
            await db.SaveChangesAsync(ct);
            logger.LogWarning("Carteira virtual MVP 1.1 restaurada pausada.");
        }
        _initialized = true;
    }

    public async Task<object> SnapshotAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await EnsureInitializedAsync(ct);
            await using var db = await factory.CreateDbContextAsync(ct);
            var account = await db.PaperAccounts.AsNoTracking().SingleAsync(x => x.Id == 1, ct);
            var positions = await db.PaperPositions.AsNoTracking()
                .OrderBy(x => x.Symbol).ToArrayAsync(ct);
            var fills = await db.PaperFills.AsNoTracking()
                .OrderByDescending(x => x.Id).Take(30).ToArrayAsync(ct);
            var agents = await db.PaperAgentCursors.AsNoTracking()
                .OrderBy(x => x.Symbol).ToArrayAsync(ct);
            return new
            {
                source = "BINANCE_SPOT_PUBLIC", mode = "PAPER_ONLY",
                realOrdersEnabled = false, testnetOrdersEnabled = false,
                strategy = RacStrategyEvaluator.Version,
                configuration = new
                {
                    startingCapitalBrl = PaperRisk.StartingCapitalBrl,
                    monthlyContributionBrl = PaperRisk.MonthlyVirtualContributionBrl,
                    riskPerTradePct = PaperRisk.MaxRiskPerTradePct,
                    maxDailyLossPct = PaperRisk.DailyLossLimitPct,
                    maxDrawdownPct = PaperRisk.MaxDrawdownPct,
                    reinvestmentPct = PaperRisk.ReinvestmentPercent,
                    maxOpenPositions = PaperRisk.MaxOpenPositions
                },
                running = account.Running, riskLocked = account.RiskLocked,
                account.RiskReason, account.CashBrl, account.LastEquityBrl,
                account.TotalContributionsBrl, account.PeakEquityBrl,
                account.EntriesToday, account.NextContributionUtc,
                account.LastCycleUtc, positions, fills, agents
            };
        }
        finally { _gate.Release(); }
    }

    public async Task<object> SetRunningAsync(bool running, string actor, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await EnsureInitializedAsync(ct);
            await using var db = await factory.CreateDbContextAsync(ct);
            var account = await db.PaperAccounts.SingleAsync(x => x.Id == 1, ct);
            if (running && account.RiskLocked)
                throw new InvalidOperationException(
                    "Carlos bloqueou a carteira virtual. É necessária revisão manual.");
            account.Running = running;
            account.UpdatedUtc = DateTimeOffset.UtcNow;
            db.AuditEvents.Add(new AuditEvent
            {
                EventType = running ? "PAPER_START" : "PAPER_PAUSE",
                Description = $"MVP 1.1, modo PAPER ONLY; comando por {actor}; nenhuma ordem Binance."
            });
            await db.SaveChangesAsync(ct);
            return new { account.Running, account.RiskLocked, account.RiskReason,
                mode = "PAPER_ONLY", realOrdersEnabled = false };
        }
        finally { _gate.Release(); }
    }

    public async Task RunCycleAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await EnsureInitializedAsync(ct);
            await using var db = await factory.CreateDbContextAsync(ct);
            var account = await db.PaperAccounts.SingleAsync(x => x.Id == 1, ct);
            if (!account.Running || account.RiskLocked) return;
            // Public data call only; never invoke BinanceSpotCredentialStore.
            var batch = await prices.GetPricesAsync(ct);
            if (!batch.Prices.TryGetValue("USDTBRL", out var rate) ||
                rate <= 0m || DateTimeOffset.UtcNow - batch.ObservedUtc > TimeSpan.FromMinutes(2))
                throw new InvalidOperationException("Sem cotação pública atual USDT/BRL.");
            var now = DateTimeOffset.UtcNow;
            var last = await db.MarketCandles.AsNoTracking()
                .Where(x => x.Interval == "1m" && x.CloseUtc >= now.AddMinutes(-180))
                .OrderByDescending(x => x.OpenUtc).Take(3000)
                .Select(x => new MarketCandlePoint(x.Symbol, x.OpenUtc, x.CloseUtc,
                    x.Open, x.High, x.Low, x.Close, x.Volume, x.QuoteVolume, x.Trades))
                .ToArrayAsync(ct);
            var groups = last.GroupBy(x => x.Symbol).ToDictionary(
                x => x.Key, x => x.OrderBy(c => c.OpenUtc)
                    .TakeLast(120).ToArray(), StringComparer.Ordinal);
            var cursors = await db.PaperAgentCursors.ToDictionaryAsync(x => x.Symbol, ct);
            var positions = await db.PaperPositions.ToListAsync(ct);

            // Fail closed: cannot value every open position, so cannot make safe decisions.
            decimal equity = account.CashBrl, exposure = 0m;
            foreach (var pos in positions)
            {
                if (!batch.Prices.TryGetValue(pos.Symbol, out var quote) || quote <= 0m)
                    throw new InvalidOperationException("Sem preço público para posição aberta.");
                var currentBrl = quote * rate;
                if (pos.LastPriceBrl <= 0m || currentBrl / pos.LastPriceBrl is < 0.8m or > 1.2m)
                    throw new InvalidOperationException("Oscilação atípica de preço; intervenção necessária.");
                pos.LastPriceBrl = currentBrl;
                var value = pos.Quantity * currentBrl;
                equity += value;
                exposure += value;
            }
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            if (account.CurrentDayUtc != today)
            {
                account.CurrentDayUtc = today;
                account.DailyStartEquityBrl = equity;
                account.EntriesToday = 0;
            }
            // Monthly virtual capital is purely fictitious and adjusts both
            // the peak and daily reference to avoid falsely reporting a gain.
            if (now >= account.NextContributionUtc)
            {
                var missed = 0;
                while (now >= account.NextContributionUtc && missed < 12)
                {
                    var contribution = PaperRisk.MonthlyVirtualContributionBrl;
                    account.CashBrl += contribution;
                    account.TotalContributionsBrl += contribution;
                    db.AuditEvents.Add(new AuditEvent
                    {
                        EventType = "PAPER_VIRTUAL_CONTRIBUTION",
                        Description = "Aporte fictício mensal de R$ 200 creditado na carteira PAPER ONLY; nenhum depósito Binance."
                    });
                    account.PeakEquityBrl += contribution;
                    account.DailyStartEquityBrl += contribution;
                    equity += contribution;
                    account.NextContributionUtc = account.NextContributionUtc.AddDays(30);
                    missed++;
                }
                if (now >= account.NextContributionUtc)
                    throw new InvalidOperationException("Muitas contribuições virtuais pendentes.");
            }
            account.PeakEquityBrl = Math.Max(account.PeakEquityBrl, equity);
            var blocked = PaperRisk.LockReason(equity,
                account.DailyStartEquityBrl, account.PeakEquityBrl);
            if (blocked != null)
            {
                LockAccount(db, account, blocked);
                account.LastEquityBrl = equity;
                await db.SaveChangesAsync(ct);
                return;
            }

            // Risk management of an open position precedes any new entry.
            foreach (var pos in positions.ToArray())
            {
                var closeUtc = groups.GetValueOrDefault(pos.Symbol)?.LastOrDefault()?.CloseUtc;
                if (closeUtc is null)
                    throw new InvalidOperationException("Sem candles para posição aberta.");
                var signal = RacStrategyEvaluator.Evaluate(pos.Symbol,
                    RacStrategyEvaluator.LatestContiguous(groups[pos.Symbol]), now);
                if (signal.Status != "READY")
                    throw new InvalidOperationException("Sinal de posição aberta inválido ou obsoleto.");
                var exitReason = pos.LastPriceBrl <= pos.StopPriceBrl
                    ? "ATR_STOP"
                    : signal.Action == "SAIDA_OBSERVACIONAL" ? "SIGNAL_EXIT" : null;
                if (exitReason is null) continue;
                var sellPrice = pos.LastPriceBrl *
                    (1m - PaperRisk.SlippagePercentPerSide / 100m);
                var gross = pos.Quantity * sellPrice;
                var fee = gross * PaperRisk.FeePercentPerSide / 100m;
                var received = gross - fee;
                account.CashBrl += received;
                var realized = received - pos.Quantity * pos.EntryPriceBrl - pos.EntryFeeBrl;
                db.PaperFills.Add(new PaperFill
                {
                    Symbol = pos.Symbol, Side = "SELL", Reason = exitReason,
                    Quantity = pos.Quantity, PriceBrl = sellPrice, FeeBrl = fee,
                    RealizedPnlBrl = realized, ExecutedUtc = now,
                    SignalClosedUtc = closeUtc.Value
                });
                db.PaperPositions.Remove(pos);
                positions.Remove(pos);
                logger.LogInformation("Paper SELL {Symbol} by {Reason}", pos.Symbol, exitReason);
            }

            equity = account.CashBrl + positions.Sum(x => x.Quantity * x.LastPriceBrl);
            exposure = positions.Sum(x => x.Quantity * x.LastPriceBrl);
            foreach (var symbol in RacMarketSymbols.Supported)
            {
                var candles = groups.GetValueOrDefault(symbol) ?? [];
                var signal = RacStrategyEvaluator.Evaluate(symbol,
                    RacStrategyEvaluator.LatestContiguous(candles), now);
                if (!cursors.TryGetValue(symbol, out var cursor))
                {
                    cursor = new PaperAgentCursor
                    {
                        Symbol = symbol, LastSignal = "SEM_SINAL", Status = "WAITING"
                    };
                    db.PaperAgentCursors.Add(cursor);
                    cursors[symbol] = cursor;
                }
                cursor.LastSignal = signal.Action;
                cursor.Status = signal.Status;
                cursor.Note = signal.Note ?? string.Empty;
                if (signal.LastClosedUtc is null ||
                    signal.LastClosedUtc <= cursor.LastCandleUtc)
                    continue;
                cursor.LastCandleUtc = signal.LastClosedUtc.Value;

                // One auditable decision for each newly processed closed candle,
                // even if a risk gate refuses a virtual entry. Never records
                // claims of actual news analysis or real Binance execution.
                var outcome = signal.Status != "READY" ? "BLOCKED_DATA" :
                    signal.Action != "COMPRA_OBSERVACIONAL" ? "NO_ENTRY_SIGNAL" :
                    positions.Count >= PaperRisk.MaxOpenPositions ? "BLOCKED_MAX_POSITIONS" :
                    "ENTRY_EVALUATED";
                db.PaperDecisionAudits.Add(new PaperDecisionAudit
                {
                    Symbol = symbol,
                    CandleClosedUtc = signal.LastClosedUtc.Value,
                    RecordedUtc = now,
                    Signal = signal.Action,
                    Outcome = outcome,
                    Actor = "Ana/Carlos",
                    Reason = outcome == "BLOCKED_DATA"
                        ? (signal.Note ?? "Dados públicos insuficientes.")
                        : "Ana: sinal técnico; Carlos: limites fixos de risco; Rebeca: notícias ainda não integradas.",
                    PlacedRealOrder = false
                });
                if (signal.Status != "READY" || signal.Action != "COMPRA_OBSERVACIONAL")
                    continue;
                if (positions.Count >= PaperRisk.MaxOpenPositions) continue;
                if (!batch.Prices.TryGetValue(symbol, out var quote) || quote <= 0m ||
                    signal.PriceUsdt is null || signal.Atr14 is null)
                    continue;
                // Refuse unusual quote-vs-closed-candle jumps rather than filling
                // a simulated market order at an unverified price.
                if (quote / signal.PriceUsdt.Value is < 0.97m or > 1.03m)
                    continue;
                var price = quote * rate * (1m + PaperRisk.SlippagePercentPerSide / 100m);
                var atr = signal.Atr14.Value * rate;
                var qty = PaperRisk.Quantity(account.CashBrl, equity, price,
                    atr, exposure, positions.Count, account.EntriesToday);
                if (qty <= 0m) continue;
                var gross = qty * price;
                var fee = gross * PaperRisk.FeePercentPerSide / 100m;
                if (gross + fee > account.CashBrl) continue;
                var pos = new PaperPosition
                {
                    Symbol = symbol, Quantity = qty,
                    EntryPriceBrl = price,
                    EntryFeeBrl = fee,
                    StopPriceBrl = Math.Max(0m, price - 2m * atr),
                    LastPriceBrl = quote * rate,
                    SignalClosedUtc = signal.LastClosedUtc.Value,
                    OpenedUtc = now
                };
                db.PaperPositions.Add(pos);
                positions.Add(pos);
                account.CashBrl -= gross + fee;
                account.EntriesToday++;
                equity -= gross + fee;
                equity += qty * pos.LastPriceBrl;
                exposure += qty * pos.LastPriceBrl;
                db.PaperFills.Add(new PaperFill
                {
                    Symbol = symbol, Side = "BUY", Reason = "SMA_RSI",
                    Quantity = qty, PriceBrl = price, FeeBrl = fee,
                    ExecutedUtc = now, SignalClosedUtc = signal.LastClosedUtc.Value
                });
                // The paper decision and the fill share the same database transaction.
                db.PaperDecisionAudits.Local.Last(x => x.Symbol == symbol &&
                    x.CandleClosedUtc == signal.LastClosedUtc.Value).Outcome = "PAPER_BUY";
                logger.LogInformation("Paper BUY {Symbol}; never a Binance order", symbol);
            }
            account.LastEquityBrl = equity;
            account.PeakEquityBrl = Math.Max(account.PeakEquityBrl, equity);
            var after = PaperRisk.LockReason(equity,
                account.DailyStartEquityBrl, account.PeakEquityBrl);
            if (after != null) LockAccount(db, account, after);
            account.LastCycleUtc = now;
            account.UpdatedUtc = now;
            await db.SaveChangesAsync(ct); // One atomic checkpoint with fills and cursors.
        }
        finally { _gate.Release(); }
    }

    public async Task HaltAsync(string reason, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            await EnsureInitializedAsync(ct);
            await using var db = await factory.CreateDbContextAsync(ct);
            var account = await db.PaperAccounts.SingleAsync(x => x.Id == 1, ct);
            LockAccount(db, account, reason);
            await db.SaveChangesAsync(ct);
        }
        finally { _gate.Release(); }
    }

    private static void LockAccount(RacDbContext db, PaperAccount account, string reason)
    {
        if (account.RiskLocked) return;
        account.RiskLocked = true;
        account.Running = false;
        account.RiskReason = reason.Length > 500 ? reason[..500] : reason;
        account.UpdatedUtc = DateTimeOffset.UtcNow;
        db.AuditEvents.Add(new AuditEvent
        {
            EventType = "PAPER_RISK_LOCK",
            Description = "Carteira exclusivamente virtual bloqueada: " + account.RiskReason
        });
    }
}

public sealed class RacPaperTradingWorker(
    RacPaperTradingService trading, ILogger<RacPaperTradingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        try
        {
            // Startup and recurring failures have the same fail-closed policy.
            // No automatic resumption, even after a transient network exception.
            await trading.RunCycleAsync(stoppingToken);
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await trading.RunCycleAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            var category = RacPaperFailureClassifier.Classify(exception);
            logger.LogCritical(exception, "Motor PAPER suspenso após falha {FailureCategory}.", category);
            try
            {
                // Only a fixed category is persisted. Never store raw exception
                // details which can contain connection strings or credentials.
                await trading.HaltAsync(RacPaperFailureClassifier.SafeReason(exception),
                    CancellationToken.None);
            }
            catch (Exception dbError)
            {
                logger.LogCritical(dbError, "Falha ao persistir trava PAPER; worker encerrado.");
            }
            // Worker remains stopped. Human risk review is mandatory.
        }
    }
}
