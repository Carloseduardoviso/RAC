using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RAC.Api.Services;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

/// <summary>Never touches the operational RAC database or places network orders.</summary>
public sealed class PaperLockedRecoveryIsolationTests
{
    [Fact]
    public async Task Restart_halt_and_attempted_start_do_not_clear_lock_or_modify_ledger()
    {
        if (!OperatingSystem.IsWindows()) return;
        var dbName = "RAC_MVP1516_LOCKED_" + Guid.NewGuid().ToString("N");
        var connection = @"Server=(localdb)\MSSQLLocalDB;Database=" + dbName +
            ";Integrated Security=True;TrustServerCertificate=True;Encrypt=True";
        var options = new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer(connection).Options;
        var now = DateTimeOffset.UtcNow;
        const string reason = "Carlos: erro anterior. Revisão obrigatória.";
        try
        {
            await using (var seed = new RacDbContext(options))
            {
                await seed.Database.EnsureCreatedAsync();
                seed.PaperAccounts.Add(new PaperAccount
                {
                    Id = 1, Running = false, RiskLocked = true, RiskReason = reason,
                    EntriesToday = 17, LastCycleUtc = now.AddMinutes(-12)
                });
                seed.PaperPositions.Add(new PaperPosition
                {
                    Symbol = "BTCUSDT", Quantity = .01m, EntryPriceBrl = 100m,
                    LastPriceBrl = 101m, StopPriceBrl = 95m,
                    SignalClosedUtc = now.AddMinutes(-12), OpenedUtc = now.AddMinutes(-12)
                });
                seed.PaperFills.Add(new PaperFill
                {
                    Symbol = "BTCUSDT", Side = "BUY", Reason = "SMA_RSI",
                    Quantity = .01m, PriceBrl = 100m,
                    ExecutedUtc = now.AddMinutes(-12), SignalClosedUtc = now.AddMinutes(-12)
                });
                seed.AuditEvents.Add(new AuditEvent { EventType = "PAPER_RISK_LOCK",
                    Description = "Existing manual risk lock remains mandatory." });
                await seed.SaveChangesAsync();
            }
            var factory = new LocalFactory(options);
            using var http = new HttpClient(new NetworkForbiddenHandler());
            var paperA = new RacPaperTradingService(factory,
                new BinancePortfolioPricesClient(http),
                NullLogger<RacPaperTradingService>.Instance);
            await paperA.RunCycleAsync(CancellationToken.None);
            await paperA.HaltAsync("different reason must not overwrite original lock",
                CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                paperA.SetRunningAsync(true, "test", CancellationToken.None));

            // New instance simulates API restart without contacting Binance.
            var paperB = new RacPaperTradingService(factory,
                new BinancePortfolioPricesClient(http),
                NullLogger<RacPaperTradingService>.Instance);
            await paperB.RunCycleAsync(CancellationToken.None);

            await using var verify = new RacDbContext(options);
            var account = await verify.PaperAccounts.SingleAsync();
            Assert.False(account.Running);
            Assert.True(account.RiskLocked);
            Assert.Equal(reason, account.RiskReason);
            Assert.Equal(17, account.EntriesToday);
            Assert.Equal(1, await verify.PaperPositions.CountAsync());
            Assert.Equal(1, await verify.PaperFills.CountAsync());
            Assert.Equal(1, await verify.AuditEvents.CountAsync());
            Assert.Equal(0, await verify.TestnetOrders.CountAsync());
        }
        finally
        {
            await using var cleanup = new RacDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private sealed class LocalFactory(DbContextOptions<RacDbContext> options)
        : IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext() => new(options);
    }

    private sealed class NetworkForbiddenHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken ct) => throw new InvalidOperationException(
                "Test must never access Binance or any network service");
    }
}
