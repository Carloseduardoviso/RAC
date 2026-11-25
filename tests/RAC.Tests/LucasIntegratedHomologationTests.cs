using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RAC.Api;
using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;
using System.Text.Json;
namespace RAC.Tests;
public sealed class LucasIntegratedHomologationTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task ScheduledPublicConnectivityProbePreservesRiskLock()
    {
        if(!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("RAC_LUCAS_LIVE_PROBE")!="1") return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_LUCAS_INTEGRATED_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            var now=DateTimeOffset.UtcNow;
            await using(var db=new RacDbContext(options))
            {
                await db.Database.MigrateAsync();
                db.PaperAccounts.Add(new PaperAccount {CashBrl=100,TotalContributionsBrl=100,LastEquityBrl=100,RiskLocked=true,Running=false});
                await db.SaveChangesAsync();
            }
            var factory=new Factory(options);
            var dispatcher=new RacSpecialistDispatcher(factory,"lucas");
            Assert.Equal(1,await dispatcher.DispatchAsync(now,default));
            Assert.Equal(0,await dispatcher.DispatchAsync(now,default));
            using var http=LucasProbeRateHandler.CreateClient();
            var registry=new LucasExchangeProbeRegistry(new ILucasExchangeProbe[]{new LucasBinancePublicClient(http),new LucasBinanceTestnetClient(http)});
            var service=new LucasConnectivityService(registry,TimeSpan.FromSeconds(60),TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(1));
            var messages=new RacAgentMessageStore(factory);
            var executor=new LucasTaskExecutor(new RacAgentTaskStore(factory),messages,new LucasConnectivityReporter(messages),service.AssessAsync);
            using var worker=new LucasConnectivityWorker(executor,NullLogger<LucasConnectivityWorker>.Instance);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(40));
            await worker.StartAsync(timeout.Token);
            try
            {
                while(true)
                {
                    await using var db=new RacDbContext(options);
                    if(await db.AgentTasks.AnyAsync(x=>x.Status=="Completed",timeout.Token)) break;
                    await Task.Delay(100,timeout.Token);
                }
            }
            finally {await worker.StopAsync(CancellationToken.None);}
            await using var check=new RacDbContext(options);
            var task=await check.AgentTasks.SingleAsync();
            var report=Assert.Single(await messages.ReadInboxAsync("ana",task.Id,10,default));
            Assert.Equal("lucas",report.Sender);
            using var json=JsonDocument.Parse(report.Content);
            var root=json.RootElement;
            Assert.False(root.GetProperty("TradingAuthorized").GetBoolean());
            Assert.Equal(2,root.GetProperty("EndpointCount").GetInt32());
            foreach(var endpoint in root.GetProperty("Endpoints").EnumerateArray())
            {
                Assert.Equal("BINANCE",endpoint.GetProperty("Exchange").GetString());
                Assert.NotEqual(JsonValueKind.Null,endpoint.GetProperty("ClockOffsetMilliseconds").ValueKind);
                Assert.DoesNotContain(endpoint.GetProperty("Findings").EnumerateArray(),x=>x.GetString()=="UNAVAILABLE" || x.GetString()=="RATE_LIMITED");
            }
            var account=await check.PaperAccounts.SingleAsync();
            Assert.True(account.RiskLocked);
            Assert.False(account.Running);
            Assert.Equal(100m,account.CashBrl);
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
