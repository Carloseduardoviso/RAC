using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class SamuelTaskExecutorSqlTests
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
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_SAMUEL_EXEC_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var factory=new Factory(options);
            var tasks=new RacAgentTaskStore(factory);
            var messages=new RacAgentMessageStore(factory);
            var reporter=new SamuelLiquidityReporter(messages);
            int reads=0;
            SamuelDepthSnapshot Book()=>new(1,DateTimeOffset.UtcNow,[new(99,1)],[new(100,1)]);
            var executor=new SamuelTaskExecutor(tasks,messages,reporter,(symbol,ct)=>{
                Assert.Equal("BTCUSDT",symbol); reads++; return Task.FromResult(Book());
            },"BTCUSDT",[1,2],1,2,2);
            var task=await tasks.EnqueueAsync("carlos","samuel","LIQUIDITY_COST_ANALYSIS",default);
            Assert.True(await executor.ExecuteNextAsync(default));
            Assert.False(await executor.ExecuteNextAsync(default));
            Assert.Single(await messages.ReadInboxAsync("carlos",task.Id,10,default));
            var retry=await tasks.EnqueueAsync("carlos","samuel","LIQUIDITY_COST_ANALYSIS",default);
            Assert.NotNull(await tasks.ClaimAsync("samuel",default));
            await reporter.PublishAsync(retry.Id,"BTCUSDT",Book(),[1,2],1,2,2,DateTimeOffset.UtcNow,default);
            await using(var db=new RacDbContext(options))
            {
                var row=await db.AgentTasks.SingleAsync(x=>x.Id==retry.Id);
                row.LeaseUntilUtc=DateTimeOffset.UtcNow.AddSeconds(-1);
                await db.SaveChangesAsync();
            }
            Assert.Equal(1,await tasks.RecoverExpiredAsync(default));
            Assert.True(await executor.ExecuteNextAsync(default));
            Assert.Equal(1,reads);
            Assert.Single(await messages.ReadInboxAsync("carlos",retry.Id,10,default));
            await using(var db=new RacDbContext(options))
                Assert.Equal(2,await db.AgentTasks.CountAsync(x=>x.Status=="Completed"));
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
