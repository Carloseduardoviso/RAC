using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
using RAC.Domain;
namespace RAC.Tests;
public sealed class RafaelTaskExecutorSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task ExecutesAndRecoversWithoutDuplicateReports()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_RAFAEL_EXEC_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options))
            {
                await db.Database.MigrateAsync();
                db.PaperAccounts.Add(new PaperAccount{RiskLocked=true});
                await db.SaveChangesAsync();
            }
            var factory=new Factory(options);
            var tasks=new RacAgentTaskStore(factory);
            var messages=new RacAgentMessageStore(factory);
            var reporter=new RafaelFinancialReporter(messages);
            var reader=new RafaelCapitalReader(factory);
            var limits=new RafaelAllocationLimits(30,40,15);
            var age=TimeSpan.FromMinutes(2);
            var executor=new RafaelTaskExecutor(tasks,messages,reporter,reader,limits,age,0.01m);
            var task=await tasks.EnqueueAsync("carlos","rafael","CAPITAL_ANALYSIS",default);
            Assert.True(await executor.ExecuteNextAsync(default));
            Assert.False(await executor.ExecuteNextAsync(default));
            Assert.Single(await messages.ReadInboxAsync("carlos",task.Id,10,default));
            var retry=await tasks.EnqueueAsync("carlos","rafael","CAPITAL_ANALYSIS",default);
            Assert.NotNull(await tasks.ClaimAsync("rafael",default));
            await reporter.PublishAsync(retry.Id,DateTimeOffset.UtcNow,await reader.ReadAsync(default),limits,age,0.01m,default);
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
                Assert.True((await db.PaperAccounts.SingleAsync()).RiskLocked);
            }
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
