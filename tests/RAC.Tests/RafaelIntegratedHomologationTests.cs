using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RAC.Application;
using RAC.Infrastructure;
using RAC.Domain;
using RAC.Api;
using System.Text.Json;
namespace RAC.Tests;
public sealed class RafaelIntegratedHomologationTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task ScheduledFinancialWorkerReportsLockedCapitalWithoutMutation()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_RAFAEL_HOMOLOG_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            var now=DateTimeOffset.UtcNow;
            await using(var db=new RacDbContext(options))
            {
                await db.Database.MigrateAsync();
                db.PaperAccounts.Add(new PaperAccount{CashBrl=80,TotalContributionsBrl=100,
                    LastEquityBrl=100,RiskLocked=true,Running=false,UpdatedUtc=now,LastCycleUtc=now});
                db.PaperPositions.Add(new PaperPosition{Symbol="BTCUSDT",Quantity=2,EntryPriceBrl=10,LastPriceBrl=10});
                await db.SaveChangesAsync();
            }
            var factory=new Factory(options);
            var dispatcher=new RacSpecialistDispatcher(factory,"rafael");
            Assert.Equal(1,await dispatcher.DispatchAsync(now,default));
            Assert.Equal(0,await dispatcher.DispatchAsync(now,default));
            var tasks=new RacAgentTaskStore(factory);
            var messages=new RacAgentMessageStore(factory);
            var executor=new RafaelTaskExecutor(tasks,messages,new RafaelFinancialReporter(messages),
                new RafaelCapitalReader(factory),new RafaelAllocationLimits(100,0,0),TimeSpan.FromSeconds(120),0.01m);
            using var worker=new RafaelCapitalWorker(executor,NullLogger<RafaelCapitalWorker>.Instance);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await worker.StartAsync(timeout.Token);
            try
            {
                while(true)
                {
                    await using var check=new RacDbContext(options);
                    if(await check.AgentTasks.AnyAsync(x=>x.Specialist=="rafael" && x.Status=="Completed",timeout.Token)) break;
                    await Task.Delay(100,timeout.Token);
                }
            }
            finally { await worker.StopAsync(CancellationToken.None); }
            var report=Assert.Single(await messages.ReadInboxAsync("carlos",null,10,default));
            Assert.Equal("rafael",report.Sender);
            using var json=JsonDocument.Parse(report.Content);
            Assert.Equal(100m,json.RootElement.GetProperty("Equity").GetDecimal());
            Assert.Equal(0m,json.RootElement.GetProperty("ResearchCapacity").GetDecimal());
            Assert.False(json.RootElement.GetProperty("TradingAuthorized").GetBoolean());
            Assert.True(json.RootElement.GetProperty("RiskLocked").GetBoolean());
            await using(var db=new RacDbContext(options))
            {
                var account=await db.PaperAccounts.SingleAsync();
                Assert.Equal(80m,account.CashBrl);
                Assert.True(account.RiskLocked);
                Assert.False(account.Running);
                Assert.Equal(2m,(await db.PaperPositions.SingleAsync()).Quantity);
                Assert.Empty(await db.PaperFills.ToListAsync());
                Assert.Empty(await db.TestnetOrders.ToListAsync());
            }
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
