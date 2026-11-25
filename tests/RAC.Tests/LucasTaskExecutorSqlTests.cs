using RAC.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
using System.Text.Json;
namespace RAC.Tests;
public sealed class LucasTaskExecutorSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task ExecutesAndRecoversExistingConnectivityReport()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_LUCAS_EXEC_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var factory=new Factory(options);
            var tasks=new RacAgentTaskStore(factory);
            var messages=new RacAgentMessageStore(factory);
            var reporter=new LucasConnectivityReporter(messages);
            var now=DateTimeOffset.UtcNow;
            var snapshot=LucasConnectivityAnalysis.Evaluate(new[]{("BINANCE","PUBLIC"),("BINANCE","TESTNET")},[],now,TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(1),TimeSpan.FromMilliseconds(500));
            var calls=0;
            var executor=new LucasTaskExecutor(tasks,messages,reporter,ct=>{calls++;return Task.FromResult(snapshot);});
            var task=await tasks.EnqueueAsync("ana","lucas","EXCHANGE_HEALTH",default);
            using(var worker=new LucasConnectivityWorker(executor,NullLogger<LucasConnectivityWorker>.Instance))
            using(var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                await worker.StartAsync(timeout.Token);
                try
                {
                    while(true)
                    {
                        await using var check=new RacDbContext(options);
                        if(await check.AgentTasks.AnyAsync(x=>x.Id==task.Id && x.Status=="Completed",timeout.Token)) break;
                        await Task.Delay(100,timeout.Token);
                    }
                }
                finally {await worker.StopAsync(CancellationToken.None);}
            }
            Assert.False(await executor.ExecuteNextAsync(default));
            var report=Assert.Single(await messages.ReadInboxAsync("ana",task.Id,10,default));
            using var json=JsonDocument.Parse(report.Content);
            Assert.False(json.RootElement.GetProperty("TradingAuthorized").GetBoolean());
            Assert.Equal("REVIEW_REQUIRED",json.RootElement.GetProperty("Status").GetString());
            var retry=await tasks.EnqueueAsync("ana","lucas","EXCHANGE_HEALTH",default);
            var old=await tasks.ClaimAsync("lucas",default);
            Assert.NotNull(old);
            await reporter.PublishAsync(retry.Id,snapshot,default);
            await using(var db=new RacDbContext(options))
            {
                var row=await db.AgentTasks.SingleAsync(x=>x.Id==retry.Id);
                row.LeaseUntilUtc=DateTimeOffset.UtcNow.AddSeconds(-1);
                await db.SaveChangesAsync();
            }
            Assert.Equal(1,await tasks.RecoverExpiredAsync(default));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>tasks.CompleteWithReportAsync(retry.Id,"lucas",old.LeaseId!.Value,null,default));
            Assert.True(await executor.ExecuteNextAsync(default));
            Assert.Equal(1,calls);
            Assert.Single(await messages.ReadInboxAsync("ana",retry.Id,10,default));
            await using(var db=new RacDbContext(options))
            {
                Assert.Equal(2,await db.AgentTasks.CountAsync(x=>x.Status=="Completed"));
                Assert.False(await db.TestnetOrders.AnyAsync());
            }
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
