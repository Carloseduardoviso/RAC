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
            await using var check=new RacDbContext(options);
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
