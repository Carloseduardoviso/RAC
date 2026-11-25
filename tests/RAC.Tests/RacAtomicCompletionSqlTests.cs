using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class RacAtomicCompletionSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task ConcurrentCompletionAndConflictingReportPreserveAtomicity()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_ATOMIC_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var factory=new Factory(options);
            var tasks=new RacAgentTaskStore(factory);
            var messages=new RacAgentMessageStore(factory);
            var task=await tasks.EnqueueAsync("ana","lucas","EXCHANGE_HEALTH",default);
            var claimed=await tasks.ClaimAsync("lucas",default);
            Assert.NotNull(claimed);
            var lease=claimed.LeaseId!.Value;
            var report=RacAgentCommunication.Create("lucas","ana",RacMessageKind.Report,
                "{\"TradingAuthorized\":false}",task.Id,DateTimeOffset.UtcNow);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>tasks.CompleteWithReportAsync(
                task.Id,"lucas",lease,report with {CorrelationId=Guid.NewGuid()},default));
            Assert.Empty(await messages.ReadInboxAsync("ana",task.Id,10,default));
            await using(var db=new RacDbContext(options))
                Assert.Equal("Running",(await db.AgentTasks.SingleAsync()).Status);
            async Task<bool> Complete()
            {
                try {await tasks.CompleteWithReportAsync(task.Id,"lucas",lease,report,default);return true;}
                catch(InvalidOperationException){return false;}
                catch(DbUpdateException){return false;}
                catch(Microsoft.Data.SqlClient.SqlException ex) when(ex.Number==1205){return false;}
            }
            var outcomes=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>Complete()));
            Assert.Single(outcomes,x=>x);
            Assert.Single(await messages.ReadInboxAsync("ana",task.Id,10,default));
            var next=await tasks.EnqueueAsync("ana","lucas","EXCHANGE_HEALTH",default);
            var nextClaim=await tasks.ClaimAsync("lucas",default);
            Assert.NotNull(nextClaim);
            // Duplicate primary key forces a database write failure, not just input rejection.
            await Assert.ThrowsAsync<DbUpdateException>(()=>tasks.CompleteWithReportAsync(
                next.Id,"lucas",nextClaim.LeaseId!.Value,report with {CorrelationId=next.Id},default));
            await using(var db=new RacDbContext(options))
            {
                Assert.Equal("Completed",(await db.AgentTasks.SingleAsync(x=>x.Id==task.Id)).Status);
                Assert.Equal("Running",(await db.AgentTasks.SingleAsync(x=>x.Id==next.Id)).Status);
                Assert.Equal(1,await db.AgentMessages.CountAsync());
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
