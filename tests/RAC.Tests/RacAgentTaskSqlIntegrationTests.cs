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
            var winner = Assert.Single(claims, x => x is not null)!;
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

    [Fact]
    public async Task RenewalProtectsActiveTaskAndRejectsRecoveredLease()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=RAC_RENEW_TEST_"+Guid.NewGuid().ToString("N")+
                ";Integrated Security=True;Encrypt=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var store=new RacAgentTaskStore(new Factory(options));
            var task=await store.EnqueueAsync("rebeca","gabriel","QUANTITATIVE_RESEARCH",default);
            var running=(await store.ClaimAsync("gabriel",default))!;
            var lease=running.LeaseId!.Value;
            var renewals=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>
                new RacAgentTaskStore(new Factory(options)).RenewAsync(task.Id,"gabriel",lease,default)));
            Assert.Contains(true,renewals);
            Assert.False(await store.RenewAsync(task.Id,"daniel",lease,default));
            Assert.Equal(0,await store.RecoverExpiredAsync(default));
            Assert.Null(await store.ClaimAsync("gabriel",default));
            await using(var db=new RacDbContext(options))
            {
                var row=await db.AgentTasks.SingleAsync();
                Assert.Equal(lease,row.LeaseId);
                Assert.Equal(1,row.Attempts);
                Assert.Equal(running.UpdatedUtc,row.UpdatedUtc);
                Assert.True(row.LeaseUntilUtc>=running.LeaseUntilUtc);
                row.LeaseUntilUtc=DateTimeOffset.UtcNow.AddSeconds(-1);
                await db.SaveChangesAsync();
            }
            Assert.False(await store.RenewAsync(task.Id,"gabriel",lease,default));
            Assert.Equal(1,await store.RecoverExpiredAsync(default));
            var retry=(await store.ClaimAsync("gabriel",default))!;
            Assert.NotEqual(lease,retry.LeaseId);
            Assert.False(await store.RenewAsync(task.Id,"gabriel",lease,default));
            var report=RacAgentCommunication.Create("gabriel","rebeca",RacMessageKind.Report,"Synthetic report",task.Id,DateTimeOffset.UtcNow);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>store.CompleteWithReportAsync(task.Id,"gabriel",lease,report,default));
            await store.CompleteWithReportAsync(task.Id,"gabriel",retry.LeaseId!.Value,report,default);
            await using var check=new RacDbContext(options);
            Assert.Equal("Completed",(await check.AgentTasks.SingleAsync()).Status);
            Assert.Equal(1,await check.AgentMessages.CountAsync());
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
