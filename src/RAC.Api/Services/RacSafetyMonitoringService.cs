using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;

namespace RAC.Api.Services;

/// <summary>
/// Request-scoped, read-only health aggregation. No order clients or execution
/// services are injected, and no SQL mutations are performed.
/// </summary>
public sealed class RacSafetyMonitoringService(IDbContextFactory<RacDbContext> factory)
{
    public async Task<RacSafetyDiagnostics.Report> SnapshotAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        var account = await db.PaperAccounts.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1, ct);
        var testnet = await db.TestnetExecutionStates.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == 1, ct);
        var latestSample = await db.PaperMonitorSamples.AsNoTracking()
            .MaxAsync(x => (DateTimeOffset?)x.RecordedUtc, ct);
        var orders = await db.TestnetOrders.CountAsync(ct);
        var paperAlerts = await db.PaperMonitorAlerts.CountAsync(
            x => x.ResolvedUtc == null, ct);
        var pending = await db.ProductionRiskPolicies.CountAsync(
            x => x.Status == "PENDING_REVIEW", ct);
        return RacSafetyDiagnostics.Evaluate(new(
            PaperPresent: account != null,
            PaperRunning: account?.Running ?? false,
            PaperRiskLocked: account?.RiskLocked ?? false,
            LastPaperCycleUtc: account?.LastCycleUtc,
            LastMonitorSampleUtc: latestSample,
            TestnetPresent: testnet != null,
            TestnetEnabled: testnet?.Enabled ?? false,
            TestnetEmergencyStopped: testnet?.EmergencyStopped ?? false,
            TestnetOrderCount: orders,
            OpenPaperAlerts: paperAlerts,
            PendingPolicyReviews: pending,
            ObservedUtc: DateTimeOffset.UtcNow));
    }
}
