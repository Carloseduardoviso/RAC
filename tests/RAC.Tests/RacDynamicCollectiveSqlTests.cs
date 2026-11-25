using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
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
            await using(var db=new RacDbContext(options))
            {
                db.PaperAccounts.Add(new PaperAccount {CashBrl=99,TotalContributionsBrl=100,LastEquityBrl=100,RiskLocked=true,Running=false});
                await db.SaveChangesAsync();
            }
            var tasks=new RacAgentTaskStore(factory);
            var service=new CarlosFinancialEvidenceService(new CarlosRafaelEvidenceReader(store),new CarlosSamuelEvidenceReader(store),new CarlosMiguelEvidenceReader(store));
            var batch=new CarlosReviewRequestBatch(factory,new CarlosReviewRequestPublisher(factory,new CarlosFinancialAdvisoryPublisher(service,store)));
            var cycle=new RacAnaAdvisoryCycle(new RacAnaRecentReview(factory),new RacAnaAdvisoryStore(factory),new RacAnaReviewRequestStore(factory),batch);
            var first=await cycle.RunAsync(DateTimeOffset.UtcNow,TimeSpan.FromMinutes(5),default);
            Assert.False(first.TradingAuthorized);
            var rafael=new RafaelTaskExecutor(tasks,store,new RafaelFinancialReporter(store),new RafaelCapitalReader(factory),new RafaelAllocationLimits(30,40,15),TimeSpan.FromMinutes(2),0.01m);
            var samuel=new SamuelTaskExecutor(tasks,store,new SamuelLiquidityReporter(store),(symbol,ct)=>Task.FromResult(new SamuelDepthSnapshot(1,DateTimeOffset.UtcNow,[new(99,1)],[new(100,1)])),"BTCUSDT",[1,2],1,2,2);
            var miguel=new MiguelTaskExecutor(tasks,store,new MiguelAuditReporter(store),new MiguelAuditReader(factory),0.01m,0.000001m,TimeSpan.FromMinutes(2));
            var lucas=new LucasTaskExecutor(tasks,store,new LucasConnectivityReporter(store),ct=>Task.FromResult(LucasConnectivityAnalysis.Evaluate(new[]{("BINANCE","PUBLIC"),("BINANCE","TESTNET")},[],DateTimeOffset.UtcNow,TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(1),TimeSpan.FromMilliseconds(500))));
            Assert.True(await rafael.ExecuteNextAsync(default));
            Assert.True(await samuel.ExecuteNextAsync(default));
            Assert.True(await miguel.ExecuteNextAsync(default));
            Assert.True(await lucas.ExecuteNextAsync(default));
            var final=await cycle.RunAsync(DateTimeOffset.UtcNow,TimeSpan.FromMinutes(5),default);
            Assert.Equal("RISK_REVIEW_REQUIRED",final.Status);
            Assert.False(final.TradingAuthorized);
            await cycle.RunAsync(DateTimeOffset.UtcNow,TimeSpan.FromMinutes(5),default);
            await using var check=new RacDbContext(options);
            Assert.Equal(2,await check.AgentMessages.CountAsync(x=>x.Kind=="TaskRequest"));
            Assert.Equal(4,await check.AgentTasks.CountAsync());
            Assert.Equal(4,await check.AgentTasks.CountAsync(x=>x.Status=="Completed"));
            Assert.Contains((await check.AgentMessages.SingleAsync(x=>x.Sender=="carlos" && x.Recipient=="ana")).Id,final.EvidenceIds);
            Assert.Contains((await check.AgentMessages.SingleAsync(x=>x.Sender=="lucas" && x.Recipient=="ana")).Id,final.EvidenceIds);
            Assert.True((await check.PaperAccounts.SingleAsync()).RiskLocked);
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
