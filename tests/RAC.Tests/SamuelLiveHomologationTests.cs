using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RAC.Api;
using RAC.Infrastructure;
using System.Text.Json;
namespace RAC.Tests;
public sealed class SamuelLiveHomologationTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task PublicDepthWorkerHomologation()
    {
        if(!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("RAC_SAMUEL_LIVE_PROBE")!="1") return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_SAMUEL_LIVE_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var factory=new Factory(options);
            var dispatcher=new RacSpecialistDispatcher(factory,"samuel");
            var now=DateTimeOffset.UtcNow;
            Assert.Equal(1,await dispatcher.DispatchAsync(now,default));
            Assert.Equal(0,await dispatcher.DispatchAsync(now,default));
            var messages=new RacAgentMessageStore(factory);
            using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){MaxResponseContentBufferSize=1024*1024};
            var client=new SamuelBinanceDepthClient(http);
            var executor=new SamuelTaskExecutor(new RacAgentTaskStore(factory),messages,
                new SamuelLiquidityReporter(messages),client.ReadAsync,"BTCUSDT",[0.0001m,0.001m],0.1m,0m,0m);
            using var worker=new SamuelLiquidityWorker(executor,NullLogger<SamuelLiquidityWorker>.Instance);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));
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
            finally { await worker.StopAsync(CancellationToken.None); }
            await using var check=new RacDbContext(options);
            var task=await check.AgentTasks.SingleAsync();
            var report=Assert.Single(await messages.ReadInboxAsync("carlos",task.Id,10,default));
            Assert.Equal("samuel",report.Sender);
            using var json=JsonDocument.Parse(report.Content);
            Assert.False(json.RootElement.GetProperty("TradingAuthorized").GetBoolean());
            Assert.True(json.RootElement.GetProperty("UpdateId").GetInt64()>0);
            Assert.Equal(2,json.RootElement.GetProperty("Scenarios").GetArrayLength());
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
