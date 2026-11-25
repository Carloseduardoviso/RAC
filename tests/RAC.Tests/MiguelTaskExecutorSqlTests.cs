using RAC.Application;
using RAC.Api;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using RAC.Infrastructure;
using RAC.Domain;
using System.Text.Json;
namespace RAC.Tests;
public sealed class MiguelTaskExecutorSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task ExecutesAuditAndRecoversPersistedReport()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_MIGUEL_EXEC_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options))
            {
                await db.Database.MigrateAsync();
                db.PaperAccounts.Add(new PaperAccount {CashBrl=99,TotalContributionsBrl=100,LastEquityBrl=100,RiskLocked=true,Running=false});
                await db.SaveChangesAsync();
            }
            var factory=new Factory(options);
            var tasks=new RacAgentTaskStore(factory);
            var messages=new RacAgentMessageStore(factory);
            var reporter=new MiguelAuditReporter(messages);
            var reader=new MiguelAuditReader(factory);
            var age=TimeSpan.FromMinutes(2);
            var executor=new MiguelTaskExecutor(tasks,messages,reporter,reader,0.01m,0.000001m,age);
            var now=DateTimeOffset.UtcNow;
            var advisory=new RacAnaAdvisoryResult("RISK_REVIEW_REQUIRED",["review"],[Guid.NewGuid()]);
            var requests=new RacAnaReviewRequestStore(factory);
            Assert.Equal(2,await requests.SaveAsync(advisory,now,default));
            var request=RacAnaReviewRequests.Create(advisory,now).Single(x=>x.Recipient=="carlos");
            var task=RacCarlosReviewTasks.Create(request.Id,now).Single(x=>x.Specialist=="miguel");
            using(var worker=new MiguelAuditWorker(executor,NullLogger<MiguelAuditWorker>.Instance))
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
                finally { await worker.StopAsync(CancellationToken.None); }
            }
            Assert.False(await executor.ExecuteNextAsync(default));
            var report=Assert.Single(await messages.ReadInboxAsync("carlos",task.Id,10,default));
            using var json=JsonDocument.Parse(report.Content);
            Assert.Equal("CRITICAL_REVIEW",json.RootElement.GetProperty("Status").GetString());
            Assert.False(json.RootElement.GetProperty("TradingAuthorized").GetBoolean());
            Assert.Equal(0,await requests.SaveAsync(advisory,now.AddSeconds(1),default));
            Assert.False(await executor.ExecuteNextAsync(default));
            var retry=await tasks.EnqueueAsync("carlos","miguel","AUDIT",default);
            Assert.NotNull(await tasks.ClaimAsync("miguel",default));
            await reporter.PublishAsync(retry.Id,await reader.ReadAsync(0.01m,0.000001m,default),DateTimeOffset.UtcNow,age,default);
            await using(var db=new RacDbContext(options))
            {
                var row=await db.AgentTasks.SingleAsync(x=>x.Id==retry.Id);
                row.LeaseUntilUtc=DateTimeOffset.UtcNow.AddSeconds(-1);
                await db.SaveChangesAsync();
            }
            Assert.Equal(1,await tasks.RecoverExpiredAsync(default));
            Assert.True(await executor.ExecuteNextAsync(default));
            Assert.Single(await messages.ReadInboxAsync("carlos",retry.Id,10,default));
            await using(var db=new RacDbContext(options))
            {
                Assert.Equal(2,await db.AgentTasks.CountAsync(x=>x.Status=="Completed"));
                var account=await db.PaperAccounts.SingleAsync();
                Assert.True(account.RiskLocked);
                Assert.False(account.Running);
                Assert.Equal(99m,account.CashBrl);
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
