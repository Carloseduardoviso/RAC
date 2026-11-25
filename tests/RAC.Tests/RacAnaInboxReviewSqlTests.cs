using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
using Xunit;

namespace RAC.Tests;
public sealed class RacAnaInboxReviewSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task ReadsPersistedEvidenceWithoutChangingMessagesOrOrders()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_ANA_REVIEW_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var store=new RacAgentMessageStore(new Factory(options));
            var now=DateTimeOffset.UtcNow;
            var id=Guid.NewGuid();
            foreach(var sender in new[]{"rebeca","carlos","lucas"})
                await store.SaveAsync(RacAgentCommunication.Create(sender,"ana",RacMessageKind.Report,"evidence",id,now),default);
            var notice=RacAgentCommunication.Create("carlos","ana",RacMessageKind.RiskNotice,"stop",id,now); await store.SaveAsync(notice,default);
            await store.SaveAsync(RacAgentCommunication.Create("lucas","ana",RacMessageKind.Report,"foreign",Guid.NewGuid(),now),default);
            var result=await new RacAnaInboxReview(store).ReviewAsync(id,now,TimeSpan.FromMinutes(5),default);
            Assert.Equal(3,result.Evidence.Count);
            Assert.Empty(result.Missing);
            Assert.True(result.RiskNoticePresent);
            var advisory=await new RacAnaAdvisoryService(new RacAnaInboxReview(store))
                .ReviewAsync(new[]{id,Guid.NewGuid(),id},now,TimeSpan.FromMinutes(5),default);
            Assert.Equal("RISK_REVIEW_REQUIRED",advisory.Status);
            Assert.False(advisory.TradingAuthorized);
            Assert.Equal(4,advisory.EvidenceIds.Count); Assert.Contains(notice.Id,advisory.EvidenceIds);
            var recent=new RacAnaRecentReview(new Factory(options));
            var automatic=await recent.ReviewAsync(now,TimeSpan.FromMinutes(5),default);
            Assert.Equal("RISK_REVIEW_REQUIRED",automatic.Status);
            Assert.Equal(5,automatic.EvidenceIds.Count); Assert.Contains(notice.Id,automatic.EvidenceIds);
            Assert.False(automatic.TradingAuthorized);
            var audit=new RacAnaAdvisoryStore(new Factory(options));
            Assert.True(await audit.SaveAsync(automatic,now,default));
            Assert.False(await audit.SaveAsync(automatic,now.AddMinutes(1),default));
            var cycle=new RacAnaAdvisoryCycle(recent,audit);
            var cycleResult=await cycle.RunAsync(now,TimeSpan.FromMinutes(5),default);
            Assert.Equal(automatic.Status,cycleResult.Status);
            Assert.False(cycleResult.TradingAuthorized);
            var expired=await recent.ReviewAsync(now.AddHours(1),TimeSpan.FromMinutes(5),default);
            Assert.Equal("INCOMPLETE_REVIEW",expired.Status);
            Assert.Empty(expired.EvidenceIds);
            await using var check=new RacDbContext(options);
            Assert.Equal(5,await check.AgentMessages.CountAsync());
            Assert.Equal(1,await check.AnaAdvisories.CountAsync()); Assert.Contains(notice.Id,System.Text.Json.JsonSerializer.Deserialize<Guid[]>((await check.AnaAdvisories.SingleAsync()).EvidenceIdsJson)!);
            var concurrent=automatic with { Reasons = ["concurrency-test"] };
            var writes=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>audit.SaveAsync(concurrent,now,default)));
            Assert.Single(writes,x=>x);
            Assert.Equal(2,await check.AnaAdvisories.CountAsync());
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
    [Fact]
    public async Task PersistsAndClassifiesRebecaCriticalMarketReportInSql()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_REBECA_ANA_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var factory=new Factory(options); var store=new RacAgentMessageStore(factory);
            var now=DateTimeOffset.UtcNow; var danielTask=Guid.NewGuid(); var gabrielTask=Guid.NewGuid();
            var daniel=RacAgentCommunication.Create("daniel","rebeca",RacMessageKind.Report,"daniel-evidence",danielTask,now);
            var gabriel=RacAgentCommunication.Create("gabriel","rebeca",RacMessageKind.Report,"{\"Mode\":\"RESEARCH_ONLY\",\"TradingAuthorized\":false}",gabrielTask,now);
            await store.SaveAsync(daniel,default); await store.SaveAsync(gabriel,default);
            var correlation=RebecaMarketAdvisoryPublisher.CreateCorrelationId(danielTask,gabrielTask);
            var market=RebecaMarketAdvisoryReport.Create(correlation,
                new("CRITICAL_REVIEW",[daniel.Id,gabriel.Id],["NEWS:BTCUSDT:HIGH_IMPACT_REVIEW"],[]),now);
            var writes=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>store.SaveMarketAdvisoryAsync(market,default)));
            Assert.Single(writes,x=>x);
            var restarted=new RacAgentMessageStore(new Factory(options));
            Assert.False(await restarted.SaveMarketAdvisoryAsync(market with {CreatedUtc=now.AddSeconds(1)},default));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>restarted.SaveMarketAdvisoryAsync(market with {Content=market.Content+" "},default));
            var provenance=new RacAnaMarketEvidenceAudit(factory);
            Assert.True(await provenance.VerifyAsync(market,RacAnaMarketReportReview.Review(market),TimeSpan.FromMinutes(5),default));
            var missing=RebecaMarketAdvisoryReport.Create(correlation,new("CRITICAL_REVIEW",[daniel.Id,Guid.NewGuid()],["missing"],[]),now);
            Assert.False(await provenance.VerifyAsync(missing,RacAnaMarketReportReview.Review(missing),TimeSpan.FromMinutes(5),default));
            var wrong=RebecaMarketAdvisoryReport.Create(Guid.NewGuid(),new("CRITICAL_REVIEW",[daniel.Id,gabriel.Id],["wrong correlation"],[]),now);
            Assert.False(await provenance.VerifyAsync(wrong,RacAnaMarketReportReview.Review(wrong),TimeSpan.FromMinutes(5),default));
            var stale=RebecaMarketAdvisoryReport.Create(correlation,new("CRITICAL_REVIEW",[daniel.Id,gabriel.Id],["stale"],[]),now.AddHours(1));
            Assert.False(await provenance.VerifyAsync(stale,RacAnaMarketReportReview.Review(stale),TimeSpan.FromMinutes(5),default));
            var altered=market with {Id=Guid.NewGuid()};
            Assert.False(await provenance.VerifyAsync(altered,RacAnaMarketReportReview.Review(altered),TimeSpan.FromMinutes(5),default));
            await store.SaveAsync(RacAgentCommunication.Create("carlos","ana",RacMessageKind.Report,"financial evidence",correlation,now),default);
            await store.SaveAsync(RacAgentCommunication.Create("lucas","ana",RacMessageKind.Report,"health evidence",correlation,now),default);
            var result=await new RacAnaRecentReview(factory).ReviewAsync(now,TimeSpan.FromMinutes(5),default);
            Assert.Equal("RISK_REVIEW_REQUIRED",result.Status);
            Assert.Contains(market.Id,result.EvidenceIds); Assert.Contains(daniel.Id,result.EvidenceIds); Assert.Contains(gabriel.Id,result.EvidenceIds);
            Assert.Contains(result.Reasons,x=>x.Contains("REBECA:CRITICAL_REVIEW:NEWS:BTCUSDT:HIGH_IMPACT_REVIEW",StringComparison.Ordinal));
            Assert.False(result.TradingAuthorized);
            var audit=new RacAnaAdvisoryStore(factory);
            Assert.True(await audit.SaveAsync(result,now,default));
            Assert.False(await audit.SaveAsync(result,now.AddMinutes(1),default));
            await using var check=new RacDbContext(options);
            Assert.Equal(5,await check.AgentMessages.CountAsync()); Assert.Equal(1,await check.AnaAdvisories.CountAsync());
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally { await using var db=new RacDbContext(options); await db.Database.EnsureDeletedAsync(); }
    }}
