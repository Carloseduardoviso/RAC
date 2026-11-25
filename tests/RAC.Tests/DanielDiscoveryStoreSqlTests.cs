using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;

public sealed class DanielDiscoveryStoreSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task StoresResearchOnceForValidLease()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_DANIEL_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;Encrypt=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var factory=new Factory(options);
            var tasks=new RacAgentTaskStore(factory);
            var store=new DanielDiscoveryStore(factory);
            var task=await tasks.EnqueueAsync("rebeca","daniel","MARKET_DISCOVERY",default);
            var running=(await tasks.ClaimAsync("daniel",default))!;
            var now=DateTimeOffset.UtcNow;
            var rows=new[]{new DanielMarketObservation("ABCUSDT",true,2000000m,100m,100.1m,3m,now)};
            await Assert.ThrowsAsync<InvalidOperationException>(()=>store.SaveAsync(task.Id,Guid.NewGuid(),rows,now,default));
            Assert.True(await store.SaveAsync(task.Id,running.LeaseId!.Value,rows,now,default));
            Assert.False(await store.SaveAsync(task.Id,running.LeaseId!.Value,rows,now,default));
            var saved=Assert.Single(await store.ReadRecentAsync(10,default));
            Assert.Equal(1,saved.CandidateCount);
            Assert.NotNull(saved.ResearchSnapshotJson);
            var snapshot=System.Text.Json.JsonSerializer.Deserialize<DanielResearchSnapshot>(saved.ResearchSnapshotJson!);
            Assert.Equal("ABCUSDT",Assert.Single(snapshot!.Decisions).Observation.Symbol);
            Assert.False(snapshot.TradingAuthorized);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>store.SaveAsync(task.Id,running.LeaseId!.Value,rows.Append(rows[0] with {Symbol="LOWUSDT",QuoteVolume24h=1}).ToArray(),now,default));
            var reporter=new DanielDiscoveryReporter(factory);
            Assert.True(await reporter.PublishAsync(task.Id,default));
            Assert.False(await reporter.PublishAsync(task.Id,default));
            var inbox=await new RacAgentMessageStore(factory).ReadInboxAsync("rebeca",task.Id,10,default);
            var report=Assert.Single(inbox);
            Assert.Equal("daniel",report.Sender);
            Assert.Contains("RESEARCH_ONLY",report.Content);
            Assert.Contains("ABCUSDT",report.Content);
            Assert.Contains("ABCUSDT",saved.CandidatesJson);
            Assert.Contains("\"TradingAuthorized\":false",saved.CandidatesJson);
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
