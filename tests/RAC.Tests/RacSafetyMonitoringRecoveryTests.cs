using Microsoft.EntityFrameworkCore;
using RAC.Api.Services;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

/// <summary>Disposable SQL recovery check. Never connects to the operational RAC DB.</summary>
public sealed class RacSafetyMonitoringRecoveryTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options)
        : IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext() => new(options);
    }

    [Fact]
    public async Task Reopen_db_and_query_diagnostics_without_changing_paper_state()
    {
        if (!OperatingSystem.IsWindows()) return;
        var name = "RAC_MVP1514_RECOVERY_" + Guid.NewGuid().ToString("N");
        var connection = @"Server=(localdb)\MSSQLLocalDB;Database=" + name +
            ";Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer(connection, sql => sql.CommandTimeout(30)).Options;
        var now = DateTimeOffset.UtcNow;
        try
        {
            await using (var db = new RacDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                db.PaperAccounts.Add(new PaperAccount {
                    Id = 1, Running = true, RiskLocked = false,
                    LastCycleUtc = now.AddSeconds(-20), UpdatedUtc = now
                });
                db.PaperPositions.Add(new PaperPosition {
                    Symbol = "BTCUSDT", Quantity = 0.01m, LastPriceBrl = 500000m,
                    OpenedUtc = now, SignalClosedUtc = now
                });
                db.PaperFills.Add(new PaperFill {
                    Symbol = "BTCUSDT", Side = "BUY", Reason = "TEST",
                    Quantity = 0.01m, PriceBrl = 500000m,
                    SignalClosedUtc = now, ExecutedUtc = now
                });
                db.PaperMonitorSamples.Add(new PaperMonitorSample {
                    RecordedUtc = now.AddSeconds(-10), Running = true
                });
                db.TestnetExecutionStates.Add(new TestnetExecutionState {
                    Id = 1, Enabled = false
                });
                await db.SaveChangesAsync();
            }
            // New DbContext instances simulate opening a fresh connection after
            // service recovery; the monitor never starts or pauses a worker.
            var monitor = new RacSafetyMonitoringService(new Factory(options));
            var first = await monitor.SnapshotAsync(CancellationToken.None);
            var afterReconnect = await monitor.SnapshotAsync(CancellationToken.None);
            Assert.Equal("OK", first.Status);
            Assert.Equal("OK", afterReconnect.Status);
            Assert.True(afterReconnect.PaperRunning);
            Assert.False(afterReconnect.TestnetEnabled);
            Assert.False(afterReconnect.RealOrdersEnabled);
            await using var verify = new RacDbContext(options);
            var account = await verify.PaperAccounts.SingleAsync();
            Assert.True(account.Running);
            Assert.False(account.RiskLocked);
            Assert.Single(await verify.PaperPositions.ToListAsync());
            Assert.Single(await verify.PaperFills.ToListAsync());
            Assert.Equal(0, await verify.TestnetOrders.CountAsync());
            Assert.Equal(1, await verify.PaperMonitorSamples.CountAsync());
        }
        finally
        {
            await using var db = new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
