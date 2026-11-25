using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
using Xunit;
namespace RAC.Tests;
public sealed class RacAnaReviewRequestStoreTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    { public RacDbContext CreateDbContext()=>new(options); }
    [Fact]
    public async Task ConcurrentRequestsPersistOnceAndSurviveNewStore()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=RAC_ANA_REQUEST_TEST_"+Guid.NewGuid().ToString("N")+";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var store=new RacAnaReviewRequestStore(new Factory(options));
            var now=DateTimeOffset.UtcNow;
            var result=new RacAnaAdvisoryResult("RISK_REVIEW_REQUIRED",["review"],[Guid.NewGuid()]);
            var writes=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>store.SaveAsync(result,now,default)));
            Assert.Equal(2,writes.Sum());
            Assert.Equal(0,await new RacAnaReviewRequestStore(new Factory(options)).SaveAsync(result,now.AddSeconds(1),default));
            await using var check=new RacDbContext(options);
            Assert.Equal(2,await check.AgentMessages.CountAsync());
            var task=await check.AgentTasks.SingleAsync(x=>x.Specialist=="lucas");
            var expected=RacCarlosReviewTasks.Create(RacAnaReviewRequests.Create(result,now).Single(x=>x.Recipient=="carlos").Id,now);
            Assert.Equal(4,await check.AgentTasks.CountAsync());
            foreach(var planned in expected)
            {
                var row=await check.AgentTasks.SingleAsync(x=>x.Id==planned.Id);
                Assert.Equal(planned.Description,row.Description);
                Assert.Equal("carlos",row.Leader);
                Assert.Equal(planned.Specialist,row.Specialist);
                Assert.Equal("Pending",row.Status);
            }
            Assert.Equal("EXCHANGE_HEALTH",task.Description);
            Assert.Equal("Pending",task.Status);
            Assert.Equal(RacAnaReviewRequests.Create(result,now).Single(x=>x.Recipient=="lucas").Id,task.Id);
            var messages=new RacAgentMessageStore(new Factory(options));
            var root=RacAgentCommunication.Create("rebeca","ana",RacMessageKind.Report,"independent",Guid.NewGuid(),now);
            Assert.True(await messages.SaveAsync(root,default));
            var seed=new RacAnaAdvisoryResult("RISK_REVIEW_REQUIRED",["first"],[root.Id]);
            Assert.Equal(2,await store.SaveIndependentAsync(seed,now,default));
            var req=RacAnaReviewRequests.Create(new RacAnaAdvisoryResult("HUMAN_REVIEW_REQUIRED",["INDEPENDENT_EVIDENCE_REVIEW"],[root.Id]),now);
            var reply=RacAgentCommunication.Create("carlos","ana",RacMessageKind.Report,"derived",req.Single(x=>x.Recipient=="carlos").CorrelationId,now);
            var health=RacAgentCommunication.Create("lucas","ana",RacMessageKind.Report,"derived",req.Single(x=>x.Recipient=="lucas").Id,now);
            Assert.True(await messages.SaveAsync(reply,default));
            Assert.True(await messages.SaveAsync(health,default));
            Assert.Equal(0,await store.SaveIndependentAsync(seed with {Reasons=["changed"],EvidenceIds=[root.Id,reply.Id,health.Id]},now.AddSeconds(1),default));
            Assert.Equal(0,await store.SaveIndependentAsync(seed with {EvidenceIds=[reply.Id,health.Id]},now.AddSeconds(2),default));
            Assert.Equal(8,await check.AgentTasks.CountAsync());
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally { await using var db=new RacDbContext(options); await db.Database.EnsureDeletedAsync(); }
    }
}
