using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RAC.Api.Services;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

/// <summary>Isolated Windows LocalDB integration: a disposable DB, never RAC.</summary>
public sealed class TestnetSqlRecoveryIntegrationTests
{
    [Fact]
    public async Task Emergency_stop_persists_without_network_and_keeps_paper_running()
    {
        if(!OperatingSystem.IsWindows())return;
        var dbName="RAC_MVP121_TEST_"+Guid.NewGuid().ToString("N");
        var connection=@"Server=(localdb)\MSSQLLocalDB;Database="+dbName+
            ";Integrated Security=True;TrustServerCertificate=True;Encrypt=True";
        var options=new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer(connection,sql=>sql.CommandTimeout(30)).Options;
        var store=new BinanceTestnetCredentialStore(Path.Combine(Path.GetTempPath(),
            "rac-mvp121-store-"+Guid.NewGuid().ToString("N")));
        using var http=new HttpClient(new ForbidAllNetwork())
            {BaseAddress=new Uri("https://testnet.binance.vision/")};
        try
        {
            await using(var db=new RacDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                db.PaperAccounts.Add(new PaperAccount{Id=1,Running=true,
                    LastCycleUtc=DateTimeOffset.UtcNow});
                db.TestnetExecutionStates.Add(new TestnetExecutionState{Id=1,Enabled=false});
                db.TestnetOrders.Add(new TestnetOrderRecord {
                    PaperFillId=91,ClientOrderId=TestnetExecutionPolicy.ClientOrderId(91),
                    Symbol="BTCUSDT",Side="BUY",Quantity=1m,Status="NEW",
                    CreatedUtc=DateTimeOffset.UtcNow,UpdatedUtc=DateTimeOffset.UtcNow
                });
                await db.SaveChangesAsync();
            }
            var executor=new RacTestnetExecutor(new IsolatedFactory(options),
                new BinanceTestnetOrderClient(http,store),
                NullLogger<RacTestnetExecutor>.Instance);
            await executor.EmergencyStopAsync(CancellationToken.None);
            await executor.EmergencyStopAsync(CancellationToken.None);
            await using(var db=new RacDbContext(options))
            {
                var state=await db.TestnetExecutionStates.SingleAsync();
                Assert.False(state.Enabled);
                Assert.True(state.EmergencyStopped);
                Assert.True(state.CircuitLocked);
                Assert.True((await db.PaperAccounts.SingleAsync()).Running);
                var saved=await db.TestnetOrders.SingleAsync();
                Assert.Equal("NEW",saved.Status);
                Assert.Contains("desativado",saved.Note,StringComparison.OrdinalIgnoreCase);
                Assert.Equal(1,await db.TestnetOrders.CountAsync());
                // Indices are enforced by the real SQL Server provider.
                db.TestnetOrders.Add(new TestnetOrderRecord {
                    PaperFillId=91,ClientOrderId=TestnetExecutionPolicy.ClientOrderId(92),
                    Symbol="BTCUSDT",Side="BUY",Quantity=1m,Status="PREPARED",
                    CreatedUtc=DateTimeOffset.UtcNow,UpdatedUtc=DateTimeOffset.UtcNow
                });
                await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());
            }
        }
        finally
        {
            // Only a randomly generated test DB can be removed; never touch RAC.
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class IsolatedFactory(DbContextOptions<RacDbContext> options)
        :IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    private sealed class ForbidAllNetwork:HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken ct) =>
            throw new InvalidOperationException("MVP 1.2.1 mock: rede proibida.");
    }
}
