using Microsoft.EntityFrameworkCore;
using RAC.Infrastructure;
namespace RAC.Tests;

public sealed class RacSpecialistDispatcherSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options) : IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }

    [Fact]
    public async Task DispatchIsIdempotentAndDoesNotAccumulatePendingWork()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_DISPATCH_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;Encrypt=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var dispatcher=new RacSpecialistDispatcher(new Factory(options));
            var now=DateTimeOffset.UtcNow;
            Assert.Equal(6,await dispatcher.DispatchAsync(now,default));
            Assert.Equal(0,await dispatcher.DispatchAsync(now,default));
            Assert.Equal(0,await dispatcher.DispatchAsync(now.AddHours(1),default));
            await using var check=new RacDbContext(options);
            Assert.Equal(6,await check.AgentTasks.CountAsync());
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
