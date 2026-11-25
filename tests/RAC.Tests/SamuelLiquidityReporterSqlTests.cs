using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
using System.Text.Json;
namespace RAC.Tests;
public sealed class SamuelLiquidityReporterSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task PersistsReportForCarlosAndDeduplicatesRetry()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_SAMUEL_REPORT_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var store=new RacAgentMessageStore(new Factory(options));
            var reporter=new SamuelLiquidityReporter(store);
            var now=DateTimeOffset.UtcNow;
            var id=Guid.NewGuid();
            var book=new SamuelDepthSnapshot(1,now,[new(99,1)],[new(100,1)]);
            Task<bool> Publish(decimal fee)=>reporter.PublishAsync(id,"BTCUSDT",book,[1,2],fee,2,2,now,default);
            Assert.True(await Publish(1));
            Assert.False(await Publish(1));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>Publish(2));
            var report=Assert.Single(await store.ReadInboxAsync("carlos",id,10,default));
            Assert.Equal("samuel",report.Sender);
            Assert.Empty(await store.ReadInboxAsync("rebeca",id,10,default));
            using var json=JsonDocument.Parse(report.Content);
            Assert.False(json.RootElement.GetProperty("TradingAuthorized").GetBoolean());
            Assert.Equal("INSUFFICIENT_DEPTH",json.RootElement.GetProperty("Scenarios")[1].GetProperty("Status").GetString());
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
