using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;

namespace RAC.Tests;
public sealed class CarlosFinancialEvidenceSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options) : IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext() => new(options);
    }

    [Fact]
    public async Task ReadsThreePersistedSpecialistReportsWithoutCreatingOrders()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_CARLOS_FINANCE_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var factory=new Factory(options);
            var store=new RacAgentMessageStore(factory);
            var now=DateTimeOffset.UtcNow;
            var rafaelTask=Guid.NewGuid();
            var samuelTask=Guid.NewGuid();
            var miguelTask=Guid.NewGuid();
            var capital=new RafaelStoredCapital(new(80,20,90,false),now,now,true,1,2,5,100);
            var rafael=RafaelFinancialReport.Create(rafaelTask,now,capital,new(30,40,15),
                TimeSpan.FromMinutes(2),0.01m);
            var book=new SamuelDepthSnapshot(1,now,[new(99,1)],[new(100,1)]);
            var samuel=SamuelLiquidityReport.Create(samuelTask,"BTCUSDT",book,[1,2],1,2,2,now);
            var audit=MiguelIntegrityAudit.Evaluate(99,100,100,[],[],0.01m,0);
            var miguel=MiguelAuditReport.Create(miguelTask,
                new MiguelStoredAudit(audit,true,false,now,now,0,0),now,TimeSpan.FromMinutes(2));
            foreach(var report in new[]{rafael,samuel,miguel})
                Assert.True(await store.SaveAsync(report,default));
            var service=new CarlosFinancialEvidenceService(
                new CarlosRafaelEvidenceReader(store),
                new CarlosSamuelEvidenceReader(store),
                new CarlosMiguelEvidenceReader(store));
            var result=await service.ReviewAsync(rafaelTask,samuelTask,miguelTask,
                now,TimeSpan.FromMinutes(5),default);
            Assert.Equal("RISK_LOCKED",result.Status);
            Assert.Equal(3,result.EvidenceIds.Count);
            Assert.Contains(rafael.Id,result.EvidenceIds);
            Assert.Contains(samuel.Id,result.EvidenceIds);
            Assert.Contains(miguel.Id,result.EvidenceIds);
            Assert.Contains("MIGUEL:RISK_LOCKED",result.Reasons);
            Assert.Contains("CROSS_CHECK:EQUITY_MISMATCH",result.Reasons);
            Assert.Contains("CROSS_CHECK:RISK_STATE_MISMATCH",result.Reasons);
            Assert.Contains("SAMUEL:REVIEW_REQUIRED",result.Reasons);
            Assert.False(result.TradingAuthorized);
            var missing=await service.ReviewAsync(rafaelTask,Guid.NewGuid(),miguelTask,
                now,TimeSpan.FromMinutes(5),default);
            Assert.Contains("SAMUEL:MISSING",missing.Reasons);
            Assert.False(missing.TradingAuthorized);
            await using var check=new RacDbContext(options);
            Assert.Equal(3,await check.AgentMessages.CountAsync());
            var publisher=new CarlosFinancialAdvisoryPublisher(service,store);
            var correlation=Guid.NewGuid();
            var published=await publisher.PublishAsync(correlation,rafaelTask,samuelTask,miguelTask,now,TimeSpan.FromMinutes(5),default);
            var retry=await publisher.PublishAsync(correlation,rafaelTask,samuelTask,miguelTask,now.AddSeconds(1),TimeSpan.FromMinutes(5),default);
            Assert.Equal(published,retry);
            var ana=await new RacAnaInboxReview(store).ReviewAsync(correlation,now.AddSeconds(1),TimeSpan.FromMinutes(5),default);
            Assert.True(ana.RiskNoticePresent);
            Assert.Contains(published.Id,ana.RiskNoticeIds);
            var financial=Assert.Single(ana.FinancialReports);
            Assert.Equal("RISK_LOCKED",financial.Status);
            Assert.Equal(3,financial.EvidenceIds.Count);
            var advisory=RacAnaAdvisory.Summarize([ana]);
            Assert.Equal("RISK_REVIEW_REQUIRED",advisory.Status);
            Assert.Contains($"{correlation}:FINANCIAL:RISK_LOCKED",advisory.Reasons);
            Assert.Contains(published.Id,advisory.EvidenceIds);
            Assert.False(advisory.TradingAuthorized);
            Assert.Equal(4,await check.AgentMessages.CountAsync());
            var concurrentId=Guid.NewGuid();
            var concurrent=await Task.WhenAll(Enumerable.Range(0,8).Select(i=>
                publisher.PublishAsync(concurrentId,rafaelTask,samuelTask,miguelTask,
                    now.AddSeconds(i),TimeSpan.FromMinutes(5),default)));
            Assert.All(concurrent,x=>Assert.Equal(concurrent[0],x));
            Assert.Equal(5,await check.AgentMessages.CountAsync());
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
