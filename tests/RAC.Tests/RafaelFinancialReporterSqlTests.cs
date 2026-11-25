using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
using System.Text.Json;
namespace RAC.Tests;

public sealed class RafaelFinancialReporterSqlTests
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
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_RAFAEL_REPORT_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var factory=new Factory(options);
            var now=DateTimeOffset.UtcNow;
            var id=Guid.NewGuid();
            var data=new RafaelStoredCapital(new(80,20,100,true),now,now,false,1,2,0,100);
            var limits=new RafaelAllocationLimits(30,40,15);
            Task<bool> Publish(RafaelStoredCapital value)=>new RafaelFinancialReporter(new RacAgentMessageStore(factory))
                .PublishAsync(id,now,value,limits,TimeSpan.FromMinutes(2),0.01m,default);
            Assert.True(await Publish(data));
            Assert.False(await Publish(data));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>Publish(data with {FillCount=3}));
            var store=new RacAgentMessageStore(factory);
            var report=Assert.Single(await store.ReadInboxAsync("carlos",id,10,default));
            Assert.Equal("rafael",report.Sender);
            Assert.Empty(await store.ReadInboxAsync("rebeca",id,10,default));
            using var json=JsonDocument.Parse(report.Content);
            Assert.False(json.RootElement.GetProperty("TradingAuthorized").GetBoolean());
            Assert.Equal(0m,json.RootElement.GetProperty("ResearchCapacity").GetDecimal());
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
