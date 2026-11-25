using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
using System.Text.Json;
namespace RAC.Tests;
public sealed class MiguelAuditReporterSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task PersistsAuditAndDeduplicatesRetries()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_MIGUEL_REPORT_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var store=new RacAgentMessageStore(new Factory(options));
            var reporter=new MiguelAuditReporter(store);
            var now=DateTimeOffset.UtcNow;
            var id=Guid.NewGuid();
            var age=TimeSpan.FromMinutes(2);
            var audit=MiguelIntegrityAudit.Evaluate(99,100,100,[],[],0.01m,0);
            var data=new MiguelStoredAudit(audit,true,false,now,now,0,0);
            Assert.True(await reporter.PublishAsync(id,data,now,age,default));
            Assert.False(await reporter.PublishAsync(id,data,now,age,default));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>reporter.PublishAsync(id,null,now,age,default));
            var report=Assert.Single(await store.ReadInboxAsync("carlos",id,10,default));
            Assert.Equal("miguel",report.Sender);
            Assert.Empty(await store.ReadInboxAsync("rebeca",id,10,default));
            using var json=JsonDocument.Parse(report.Content);
            Assert.False(json.RootElement.GetProperty("TradingAuthorized").GetBoolean());
            Assert.True(json.RootElement.GetProperty("HumanReviewRequired").GetBoolean());
            Assert.Equal("CRITICAL_REVIEW",json.RootElement.GetProperty("Status").GetString());
            var missingId=Guid.NewGuid();
            Assert.True(await reporter.PublishAsync(missingId,null,now,age,default));
            Assert.Single(await store.ReadInboxAsync("carlos",missingId,10,default));
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
