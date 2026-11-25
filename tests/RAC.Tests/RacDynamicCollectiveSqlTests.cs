using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace RAC.Tests;

public sealed class RacDynamicCollectiveSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuditsPersistedDynamicReports(bool incompatible)
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_DYNAMIC_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var factory=new Factory(options);
            var store=new RacAgentMessageStore(factory);
            var now=DateTimeOffset.UtcNow;
            var d=Guid.NewGuid(); var g=Guid.NewGuid();
            var daniel=RacAgentCommunication.Create("daniel","rebeca",RacMessageKind.Report,JsonSerializer.Serialize(new {
                Mode="RESEARCH_ONLY",TradingAuthorized=false,TaskId=d,ObservedUtc=now,
                CoverageStatus="RESEARCH_CANDIDATES_AVAILABLE",
                Candidates=new[]{new {Symbol="ABCUSDT",Rank=1,QuoteVolume24h=2000000m,SpreadPercent=0.1m,TradingAuthorized=false}}
            }),d,now);
            var gabriel=RacAgentCommunication.Create("gabriel","rebeca",RacMessageKind.Report,JsonSerializer.Serialize(new {
                Schema="GABRIEL_DYNAMIC_V2",Mode="RESEARCH_ONLY",TradingAuthorized=false,IndependentValidation=false,
                Symbols=new[]{"ABCUSDT"},Configurations=24,StressRanked=12,SourceSha256=new string('A',64),
                PositiveMean=0,FurtherResearch=0,Findings=new[]{new {Status="NON_POSITIVE_NET_RETURN",Count=12}},
                DanielTaskId=d,DanielMessageId=incompatible?Guid.NewGuid():daniel.Id,
                DiscoveryAssessments=new[]{new {Symbol="ABCUSDT",Status="NO_QUALIFYING_CONFIGURATION",StressConfigurations=12,FurtherResearch=0,TradingAuthorized=false}}
            }),g,now) with {Id=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("GABRIEL:REPORT:V2:"+g.ToString("N"))).AsSpan(0,16))};
            await store.SaveAsync(daniel,default);
            await store.SaveAsync(gabriel,default);
            var calendar=new RebecaNewsCalendar();
            calendar.Publish(now,[]);
            var publisher=new RebecaMarketAdvisoryPublisher(new RebecaMarketAdvisory(new RebecaMarketEvidenceReader(store),calendar),store);
            var publication=await publisher.ReviewAndPublishAsync(d,g,now,TimeSpan.FromMinutes(5),default);
            Assert.True(publication.Published);
            Assert.Equal(incompatible?"CRITICAL_REVIEW":"HUMAN_REVIEW_REQUIRED",publication.Review.Status);
            Assert.False(publication.TradingAuthorized);
            Assert.False((await publisher.ReviewAndPublishAsync(d,g,now,TimeSpan.FromMinutes(5),default)).Published);
            var market=RebecaMarketAdvisoryReport.Create(publication.CorrelationId,publication.Review,now);
            Assert.Equal(publication.MessageId,market.Id);
            Assert.Equal(!incompatible,await new RacAnaMarketEvidenceAudit(factory).VerifyAsync(market,RacAnaMarketReportReview.Review(market),TimeSpan.FromMinutes(5),default));
            await using var check=new RacDbContext(options);
            Assert.Equal(3,await check.AgentMessages.CountAsync());
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
