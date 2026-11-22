using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Api.Services;

/// <summary>
/// Independent watchdog and report for PAPER ONLY. Never reads private Binance
/// credentials, sends orders, or resets a Carlos risk lock.
/// </summary>
public sealed class RacPaperMonitoringService(
    IDbContextFactory<RacDbContext> factory,
    RacPaperTradingService paper,
    ILogger<RacPaperMonitoringService> logger)
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task ObserveAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            // Force the paper account's safe paused-on-restart restoration
            // before the watchdog checks whether a cycle is late.
            await paper.SnapshotAsync(ct);
            await using var db = await factory.CreateDbContextAsync(ct);
            var account = await db.PaperAccounts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == 1, ct);
            if (account is null) return; // separate paper worker initializes safely.

            var now = DateTimeOffset.UtcNow;
            var agents = await db.PaperAgentCursors.AsNoTracking()
                .OrderBy(x => x.Symbol).ToListAsync(ct);
            var latestCandle = await db.MarketCandles.AsNoTracking()
                .Where(x => x.Interval == "1m")
                .MaxAsync(x => (DateTimeOffset?)x.CloseUtc, ct);
            var positions = await db.PaperPositions.AsNoTracking().ToArrayAsync(ct);
            var incidents = PaperMonitoringPolicy.Evaluate(account, agents, latestCandle, now);

            // Do not duplicate the same incident at every poll. Resolved incidents
            // remain immutable historical records; reopening creates a new row.
            var open = await db.PaperMonitorAlerts
                .Where(x => x.ResolvedUtc == null).ToListAsync(ct);
            foreach (var alert in open)
            {
                if (incidents.TryGetValue(alert.Code, out var occurrence))
                {
                    // Refresh timestamp once per minute so the dashboard can
                    // distinguish a continuously observed incident from a stale one.
                    if (now - alert.LastObservedUtc >= TimeSpan.FromSeconds(50))
                    {
                        alert.LastObservedUtc = now;
                        alert.Observations++;
                    }
                    alert.Severity = occurrence.Severity;
                    alert.Message = occurrence.Message;
                }
                else alert.ResolvedUtc = now;
            }
            foreach (var occurrence in incidents)
            {
                if (open.Any(x => x.Code == occurrence.Key)) continue;
                db.PaperMonitorAlerts.Add(new PaperMonitorAlert
                {
                    Code = occurrence.Key, Severity = occurrence.Value.Severity,
                    Message = occurrence.Value.Message,
                    OpenedUtc = now, LastObservedUtc = now,
                    Observations = 1
                });
                logger.LogWarning("PAPER monitor incident {Code}", occurrence.Key);
            }

            var lastSample = await db.PaperMonitorSamples.AsNoTracking()
                .OrderByDescending(x => x.Id)
                .Select(x => (DateTimeOffset?)x.RecordedUtc).FirstOrDefaultAsync(ct);
            if (lastSample is null || now - lastSample >= TimeSpan.FromSeconds(50))
            {
                db.PaperMonitorSamples.Add(new PaperMonitorSample
                {
                    RecordedUtc = now,
                    EquityBrl = account.LastEquityBrl,
                    CashBrl = account.CashBrl,
                    ExposureBrl = positions.Sum(x => x.Quantity * x.LastPriceBrl),
                    TotalContributionsBrl = account.TotalContributionsBrl,
                    DrawdownPercent = PaperMonitoringPolicy.DrawdownPercent(
                        account.LastEquityBrl, account.PeakEquityBrl),
                    ReadyAgents = agents.Count(x => x.Status == "READY"),
                    BlockedAgents = Math.Max(0, RacMarketSymbols.Supported.Length -
                        agents.Count(x => x.Status == "READY")),
                    OpenPositions = positions.Length,
                    Running = account.Running, RiskLocked = account.RiskLocked,
                    LastPaperCycleUtc = account.LastCycleUtc
                });
            }

            // Bound monitoring storage; decisions and fills are audit records
            // and are NOT automatically deleted by this housekeeping.
            var retention = now.AddDays(-14);
            await db.PaperMonitorSamples
                .Where(x => x.RecordedUtc < retention).ExecuteDeleteAsync(ct);
            await db.SaveChangesAsync(ct);
        }
        finally { _gate.Release(); }
    }

    public async Task<object> ReportAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var account = await db.PaperAccounts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1, ct);
        var now = DateTimeOffset.UtcNow;
        var samples = await db.PaperMonitorSamples.AsNoTracking()
            .OrderByDescending(x => x.RecordedUtc).Take(120).ToListAsync(ct);
        var alerts = await db.PaperMonitorAlerts.AsNoTracking()
            .OrderByDescending(x => x.OpenedUtc).Take(40).ToListAsync(ct);
        var decisions = await db.PaperDecisionAudits.AsNoTracking()
            .OrderByDescending(x => x.RecordedUtc).ThenByDescending(x => x.Id)
            .Take(100).ToListAsync(ct);
        var fillStats = await db.PaperFills.AsNoTracking()
            .GroupBy(x => 1).Select(g => new
            {
                Count = g.Count(),
                Realized = g.Sum(x => x.RealizedPnlBrl ?? 0m),
                Fees = g.Sum(x => x.FeeBrl),
                Closed = g.Count(x => x.Side == "SELL")
            }).FirstOrDefaultAsync(ct);
        var agents = await db.PaperAgentCursors.AsNoTracking()
            .OrderBy(x => x.Symbol).ToListAsync(ct);
        var positions = await db.PaperPositions.AsNoTracking().ToListAsync(ct);
        var realized = fillStats?.Realized ?? 0m;
        var fees = fillStats?.Fees ?? 0m;
        var latest = samples.FirstOrDefault();
        return new
        {
            mode = "PAPER_ONLY", source = "BINANCE_SPOT_PUBLIC",
            realOrdersEnabled = false, testnetOrdersEnabled = false,
            observedUtc = now, running = account?.Running ?? false,
            riskLocked = account?.RiskLocked ?? false,
            riskReason = account?.RiskReason,
            performance = new
            {
                equityBrl = account?.LastEquityBrl ?? 0m,
                cashBrl = account?.CashBrl ?? 0m,
                contributionsBrl = account?.TotalContributionsBrl ?? 0m,
                pnlExcludingContributionsBrl =
                    (account?.LastEquityBrl ?? 0m) - (account?.TotalContributionsBrl ?? 0m),
                realizedPnlBrl = realized, feesBrl = fees,
                drawdownPct = account is null ? 0m :
                    PaperMonitoringPolicy.DrawdownPercent(
                        account.LastEquityBrl, account.PeakEquityBrl),
                virtualFills = fillStats?.Count ?? 0,
                closedPositions = fillStats?.Closed ?? 0,
                openPositions = positions.Count,
                paperCycleUtc = account?.LastCycleUtc,
                latestMonitorUtc = latest?.RecordedUtc
            },
            agentStates = agents.Select(x => new {
                x.Symbol, x.LastSignal, x.Status, x.LastCandleUtc, x.Note }),
            alerts = alerts.Select(x => new {
                x.Code, x.Severity, x.Message, x.OpenedUtc,
                x.LastObservedUtc, x.ResolvedUtc, x.Observations,
                active = x.ResolvedUtc is null
            }),
            decisions = decisions.Select(x => new {
                x.Symbol, x.Signal, x.Outcome, x.Actor,
                x.Reason, x.CandleClosedUtc, x.RecordedUtc, x.PlacedRealOrder
            }),
            history = samples.OrderBy(x => x.RecordedUtc).Select(x => new {
                x.RecordedUtc, x.EquityBrl, x.CashBrl, x.ExposureBrl,
                x.DrawdownPercent, x.ReadyAgents, x.BlockedAgents,
                x.Running, x.RiskLocked
            })
        };
    }
}

/// <summary>Separate watcher survives the paper execution worker stopping.</summary>
public sealed class RacPaperMonitoringWorker(
    RacPaperMonitoringService monitor, ILogger<RacPaperMonitoringWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
        try
        {
            // Check immediately, then every minute. Unlike trading worker,
            // monitoring continues after recoverable database/network failures.
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await monitor.ObserveAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                { break; }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Monitor PAPER indisponível; nenhum bloqueio foi desativado.");
                }
                if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
