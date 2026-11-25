using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RAC.Api;
using RAC.Domain;
using RAC.Infrastructure;
using System.Text.Json;
namespace RAC.Tests;
public sealed class MiguelIntegratedHomologationTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task ScheduledAuditReconcilesPortfolioAndPreservesRiskLock()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_MIGUEL_INTEGRATED_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            var now=DateTimeOffset.UtcNow;
            await using(var db=new RacDbContext(options))
            {
                await db.Database.MigrateAsync();
                db.PaperAccounts.Add(new PaperAccount {CashBrl=79,TotalContributionsBrl=100,LastEquityBrl=103,RiskLocked=true,Running=false,UpdatedUtc=now,LastCycleUtc=now});
                db.PaperFills.Add(new PaperFill {Symbol="BTCUSDT",Side="BUY",Reason="TEST",Quantity=2,PriceBrl=10,FeeBrl=1,ExecutedUtc=now,SignalClosedUtc=now});
                db.PaperPositions.Add(new PaperPosition {Symbol="BTCUSDT",Quantity=2,EntryPriceBrl=10,EntryFeeBrl=1,LastPriceBrl=12,StopPriceBrl=9,OpenedUtc=now,SignalClosedUtc=now});
                await db.SaveChangesAsync();
            }
            var factory=new Factory(options);
            var dispatcher=new RacSpecialistDispatcher(factory,"miguel");
            Assert.Equal(1,await dispatcher.DispatchAsync(now,default));
            Assert.Equal(0,await dispatcher.DispatchAsync(now,default));
            var messages=new RacAgentMessageStore(factory);
            var executor=new MiguelTaskExecutor(new RacAgentTaskStore(factory),messages,new MiguelAuditReporter(messages),
                new MiguelAuditReader(factory),0.01m,0.000001m,TimeSpan.FromMinutes(2));
            using var worker=new MiguelAuditWorker(executor,NullLogger<MiguelAuditWorker>.Instance);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
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
            var report=Assert.Single(await messages.ReadInboxAsync("carlos",task.Id,10,default));
            Assert.Equal("miguel",report.Sender);
            using var json=JsonDocument.Parse(report.Content);
            var root=json.RootElement;
            Assert.True(root.GetProperty("AccountingConsistent").GetBoolean());
            Assert.True(root.GetProperty("HumanReviewRequired").GetBoolean());
            Assert.False(root.GetProperty("TradingAuthorized").GetBoolean());
            Assert.Equal(103m,root.GetProperty("CalculatedEquity").GetDecimal());
            Assert.Equal(3m,root.GetProperty("Unrealized").GetDecimal());
            Assert.Equal(0m,root.GetProperty("ProfitDifference").GetDecimal());
            var account=await check.PaperAccounts.SingleAsync();
            Assert.True(account.RiskLocked);
            Assert.False(account.Running);
            Assert.Equal(79m,account.CashBrl);
            Assert.Equal(1,await check.PaperFills.CountAsync());
            Assert.Equal(1,await check.PaperPositions.CountAsync());
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
