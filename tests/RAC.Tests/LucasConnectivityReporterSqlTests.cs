using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
using System.Text.Json;
namespace RAC.Tests;
public sealed class LucasConnectivityReporterSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task PersistsConnectivityReportsForAnaAndDeduplicatesRetries()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_LUCAS_REPORT_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var store=new RacAgentMessageStore(new Factory(options));
            var reporter=new LucasConnectivityReporter(store);
            var now=DateTimeOffset.UtcNow;
            var id=Guid.NewGuid();
            var expected=new[]{("BINANCE","PUBLIC"),("BINANCE","TESTNET")};
            var snapshot=LucasConnectivityAnalysis.Evaluate(expected,[],now,TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(1),TimeSpan.FromMilliseconds(500));
            Assert.True(await reporter.PublishAsync(id,snapshot,default));
            Assert.False(await reporter.PublishAsync(id,snapshot,default));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>reporter.PublishAsync(id,snapshot with {ObservedUtc=now.AddSeconds(1)},default));
            var report=Assert.Single(await store.ReadInboxAsync("ana",id,10,default));
            Assert.Equal("lucas",report.Sender);
            Assert.Empty(await store.ReadInboxAsync("carlos",id,10,default));
            using var json=JsonDocument.Parse(report.Content);
            Assert.False(json.RootElement.GetProperty("TradingAuthorized").GetBoolean());
            Assert.True(json.RootElement.GetProperty("HumanReviewRequired").GetBoolean());
            Assert.Equal("REVIEW_REQUIRED",json.RootElement.GetProperty("Status").GetString());
            var nextId=Guid.NewGuid();
            Assert.True(await reporter.PublishAsync(nextId,snapshot,default));
            Assert.Single(await store.ReadInboxAsync("ana",nextId,10,default));
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
