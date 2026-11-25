using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
using Xunit;
namespace RAC.Tests;
public sealed class RacAnaReviewRequestStoreTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    { public RacDbContext CreateDbContext()=>new(options); }
    [Fact]
    public async Task ConcurrentRequestsPersistOnceAndSurviveNewStore()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=RAC_ANA_REQUEST_TEST_"+Guid.NewGuid().ToString("N")+";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var store=new RacAnaReviewRequestStore(new Factory(options));
            var now=DateTimeOffset.UtcNow;
            var result=new RacAnaAdvisoryResult("RISK_REVIEW_REQUIRED",["review"],[Guid.NewGuid()]);
            var writes=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>store.SaveAsync(result,now,default)));
            Assert.Equal(2,writes.Sum());
            Assert.Equal(0,await new RacAnaReviewRequestStore(new Factory(options)).SaveAsync(result,now.AddSeconds(1),default));
            await using var check=new RacDbContext(options);
            Assert.Equal(2,await check.AgentMessages.CountAsync());
            var task=await check.AgentTasks.SingleAsync();
            Assert.Equal("EXCHANGE_HEALTH",task.Description);
            Assert.Equal("Pending",task.Status);
            Assert.Equal(RacAnaReviewRequests.Create(result,now).Single(x=>x.Recipient=="lucas").Id,task.Id);
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally { await using var db=new RacDbContext(options); await db.Database.EnsureDeletedAsync(); }
    }
}
