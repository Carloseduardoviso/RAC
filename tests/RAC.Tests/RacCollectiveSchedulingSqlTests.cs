using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class RacCollectiveSchedulingSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task SixSpecialistsReceiveDistinctTasksWithCorrectLeadership()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_COLLECTIVE_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var factory=new Factory(options);
            var dispatcher=new RacSpecialistDispatcher(factory,"daniel","gabriel","rafael","samuel","miguel","lucas");
            var now=DateTimeOffset.UtcNow;
            Assert.Equal(6,await dispatcher.DispatchAsync(now,default));
            Assert.Equal(0,await dispatcher.DispatchAsync(now,default));
            Assert.Equal(0,await dispatcher.DispatchAsync(now.AddHours(2),default));
            var tasks=new RacAgentTaskStore(factory);
            foreach(var schedule in RacSpecialistSchedulePlan.Entries)
            {
                var task=await tasks.ClaimAsync(schedule.Specialist,default);
                Assert.NotNull(task);
                Assert.Equal(schedule.Leader,task.Leader);
                Assert.Equal(schedule.Activity,task.Description);
                Assert.Equal(RacSpecialistSchedulePlan.ForWindow(schedule,now).Id,task.Id);
                Assert.Null(await tasks.ClaimAsync(schedule.Specialist,default));
                await tasks.FinishAsync(task.Id,schedule.Specialist,task.LeaseId!.Value,true,default);
            }
            Assert.Equal(0,await dispatcher.DispatchAsync(now,default));
            Assert.Equal(6,await dispatcher.DispatchAsync(now.AddHours(2),default));
            await using var check=new RacDbContext(options);
            Assert.Equal(6,await check.AgentTasks.CountAsync(x=>x.Status=="Completed"));
            Assert.Equal(6,await check.AgentTasks.CountAsync(x=>x.Status=="Pending"));
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
