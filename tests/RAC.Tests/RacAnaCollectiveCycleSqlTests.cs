using Microsoft.EntityFrameworkCore;
using RAC.Api;
using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class RacAnaCollectiveCycleSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    { public RacDbContext CreateDbContext()=>new(options); }
    [Fact]
    public async Task IndependentEvidenceRunsFourSpecialistsAndReturnsWithoutRecursiveRequests()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=RAC_COLLECTIVE_TEST_"+Guid.NewGuid().ToString("N")+";Integrated Security=True;TrustServerCertificate=True").Options;
        var factory=new Factory(options);
        try
        {
            await using(var db=new RacDbContext(options))
            {
                await db.Database.MigrateAsync();
                db.PaperAccounts.Add(new PaperAccount {CashBrl=99,TotalContributionsBrl=100,LastEquityBrl=100,RiskLocked=true,Running=false});
                await db.SaveChangesAsync();
            }
            var tasks=new RacAgentTaskStore(factory);
            var messages=new RacAgentMessageStore(factory);
            var service=new CarlosFinancialEvidenceService(new CarlosRafaelEvidenceReader(messages),new CarlosSamuelEvidenceReader(messages),new CarlosMiguelEvidenceReader(messages));
            var batch=new CarlosReviewRequestBatch(factory,new CarlosReviewRequestPublisher(factory,new CarlosFinancialAdvisoryPublisher(service,messages)));
            var cycle=new RacAnaAdvisoryCycle(new RacAnaRecentReview(factory),new RacAnaAdvisoryStore(factory),new RacAnaReviewRequestStore(factory),batch);
            var now=DateTimeOffset.UtcNow;
            var root=RacAgentCommunication.Create("rebeca","ana",RacMessageKind.Report,"independent review",Guid.NewGuid(),now);
            Assert.True(await messages.SaveAsync(root,default));
            var first=await cycle.RunAsync(now,TimeSpan.FromMinutes(5),default);
            Assert.False(first.TradingAuthorized);
            var rafael=new RafaelTaskExecutor(tasks,messages,new RafaelFinancialReporter(messages),new RafaelCapitalReader(factory),new RafaelAllocationLimits(30,40,15),TimeSpan.FromMinutes(2),0.01m);
            var samuel=new SamuelTaskExecutor(tasks,messages,new SamuelLiquidityReporter(messages),(symbol,ct)=>Task.FromResult(new SamuelDepthSnapshot(1,DateTimeOffset.UtcNow,[new(99,1)],[new(100,1)])),"BTCUSDT",[1,2],1,2,2);
            var miguel=new MiguelTaskExecutor(tasks,messages,new MiguelAuditReporter(messages),new MiguelAuditReader(factory),0.01m,0.000001m,TimeSpan.FromMinutes(2));
            var lucas=new LucasTaskExecutor(tasks,messages,new LucasConnectivityReporter(messages),ct=>Task.FromResult(LucasConnectivityAnalysis.Evaluate(new[]{("BINANCE","PUBLIC"),("BINANCE","TESTNET")},[],DateTimeOffset.UtcNow,TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(1),TimeSpan.FromMilliseconds(500))));
            Assert.True(await rafael.ExecuteNextAsync(default));
            Assert.True(await samuel.ExecuteNextAsync(default));
            Assert.True(await miguel.ExecuteNextAsync(default));
            Assert.True(await lucas.ExecuteNextAsync(default));
            var final=await cycle.RunAsync(DateTimeOffset.UtcNow,TimeSpan.FromMinutes(5),default);
            Assert.False(final.TradingAuthorized);
            Assert.Equal("RISK_REVIEW_REQUIRED",final.Status);
            await using var check=new RacDbContext(options);
            var requests=await check.AgentMessages.Where(x=>x.Kind=="TaskRequest").ToListAsync();
            Assert.Equal(2,requests.Count);
            var financial=await check.AgentMessages.SingleAsync(x=>x.Sender=="carlos" && x.Recipient=="ana");
            var health=await check.AgentMessages.SingleAsync(x=>x.Sender=="lucas" && x.Recipient=="ana");
            Assert.Equal(requests.Single(x=>x.Recipient=="carlos").CorrelationId,financial.CorrelationId);
            Assert.Equal(requests.Single(x=>x.Recipient=="lucas").Id,health.CorrelationId);
            Assert.Contains(financial.Id,final.EvidenceIds);
            Assert.Contains(health.Id,final.EvidenceIds);
            Assert.Equal(4,await check.AgentTasks.CountAsync(x=>x.Status=="Completed"));
            await cycle.RunAsync(DateTimeOffset.UtcNow,TimeSpan.FromMinutes(5),default);
            Assert.Equal(4,await check.AgentTasks.CountAsync());
            Assert.Equal(2,await check.AgentMessages.CountAsync(x=>x.Kind=="TaskRequest"));
            Assert.True((await check.PaperAccounts.SingleAsync()).RiskLocked);
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally { await using var db=new RacDbContext(options); await db.Database.EnsureDeletedAsync(); }
    }
}
