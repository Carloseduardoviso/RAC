using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;

namespace RAC.Tests;

public sealed class RacAgentTaskSqlIntegrationTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options) : IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext() => new(options);
    }

    [Fact]
    public async Task ConcurrentClaimsAndExpiredLeaseRecovery()
    {
        if (!OperatingSystem.IsWindows()) return;
        var name = "RAC_AGENT_TASK_TEST_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=" + name +
                ";Integrated Security=True;Encrypt=True;TrustServerCertificate=True").Options;
        try
        {
            await using (var db = new RacDbContext(options))
                await db.Database.MigrateAsync();
            var store = new RacAgentTaskStore(new Factory(options));
            var task = await store.EnqueueAsync("rebeca","daniel","Synthetic research",default);
            var claims = await Task.WhenAll(Enumerable.Range(0,8).Select(_ =>
                new RacAgentTaskStore(new Factory(options)).ClaimAsync("daniel",default)));
            var winner = Assert.Single(claims.Where(x => x is not null))!;
            Assert.Equal(task.Id,winner.Id);
            Assert.Null(await store.ClaimAsync("gabriel",default));
            Assert.Equal(0,await store.RecoverExpiredAsync(default));
            await using (var db = new RacDbContext(options))
            {
                var row = await db.AgentTasks.SingleAsync();
                row.UpdatedUtc = DateTimeOffset.UtcNow.AddMinutes(-10);
                row.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(-5);
                await db.SaveChangesAsync();
            }
            Assert.Equal(1,await store.RecoverExpiredAsync(default));
            var retry = (await store.ClaimAsync("daniel",default))!;
            Assert.Equal(2,retry.Attempts);
            Assert.NotEqual(winner.LeaseId,retry.LeaseId);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.FinishAsync(task.Id,"daniel",winner.LeaseId!.Value,true,default));
            var done = await store.FinishAsync(task.Id,"daniel",retry.LeaseId!.Value,true,default);
            Assert.Equal(RacTaskStatus.Completed,done.Status);
            Assert.Null(await store.ClaimAsync("daniel",default));
        }
        finally
        {
            await using var db = new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
