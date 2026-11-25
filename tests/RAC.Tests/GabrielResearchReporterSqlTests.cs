using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace RAC.Tests;

public sealed class GabrielResearchReporterSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task PersistsReportAndDeduplicatesRetry()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_GABRIEL_REPORT_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;Encrypt=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var months=new[]{"2026-05","2026-06","2026-07","2026-08","2026-09","2026-10"};
            var rows=ResearchExperimentPlan.Build().SelectMany(p=>months.Select(m=>
                new GabrielExperimentMatrix.Row(m,"EXPLORATORY",p.Symbol,p.Parameters,new(-1m,2m,5,2,0.5m,1m,false)))).ToArray();
            var json=JsonSerializer.Serialize(new {Mode="RESEARCH_ONLY",TradingAuthorized=false,IndependentValidation=false,Rows=rows});
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
            var id=Guid.NewGuid();var now=DateTimeOffset.UtcNow;
            var store=new RacAgentMessageStore(new Factory(options));
            Assert.True(await new GabrielResearchReporter(store).PublishAsync(id,now,hash,json,default));
            var recovered=new RacAgentMessageStore(new Factory(options));
            Assert.False(await new GabrielResearchReporter(recovered).PublishAsync(id,now,hash,json,default));
            var inbox=await recovered.ReadInboxAsync("rebeca",id,10,default);
            var report=Assert.Single(inbox);
            Assert.Equal("gabriel",report.Sender);
            using var doc=JsonDocument.Parse(report.Content);
            Assert.False(doc.RootElement.GetProperty("TradingAuthorized").GetBoolean());
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
