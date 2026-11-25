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
            await store.SaveAsync(RacAgentCommunication.Create("carlos","ana",RacMessageKind.RiskNotice,"stop",id,now),default);
            await store.SaveAsync(RacAgentCommunication.Create("lucas","ana",RacMessageKind.Report,"foreign",Guid.NewGuid(),now),default);
            var result=await new RacAnaInboxReview(store).ReviewAsync(id,now,TimeSpan.FromMinutes(5),default);
            Assert.Equal(3,result.Evidence.Count);
            Assert.Empty(result.Missing);
            Assert.True(result.RiskNoticePresent);
            var advisory=await new RacAnaAdvisoryService(new RacAnaInboxReview(store))
                .ReviewAsync(new[]{id,Guid.NewGuid(),id},now,TimeSpan.FromMinutes(5),default);
            Assert.Equal("RISK_REVIEW_REQUIRED",advisory.Status);
            Assert.False(advisory.TradingAuthorized);
            Assert.Equal(3,advisory.EvidenceIds.Count);
            var recent=new RacAnaRecentReview(new Factory(options));
            var automatic=await recent.ReviewAsync(now,TimeSpan.FromMinutes(5),default);
            Assert.Equal("RISK_REVIEW_REQUIRED",automatic.Status);
            Assert.Equal(4,automatic.EvidenceIds.Count);
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
            Assert.Equal(1,await check.AnaAdvisories.CountAsync());
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
}
