using RAC.Application;
namespace RAC.Tests;

public class RacTaskLifecycleTests
{
    [Fact]
    public void OnlyAssignedSpecialistCanCompleteWithCurrentLease()
    {
        var now = DateTimeOffset.UtcNow;
        var task = RacTaskLifecycle.Create("rebeca","daniel","Research",now);
        Assert.Throws<InvalidOperationException>(() => RacTaskLifecycle.Start(task,"gabriel",now));
        var running = RacTaskLifecycle.Start(task,"daniel",now);
        Assert.Throws<InvalidOperationException>(() => RacTaskLifecycle.Finish(running,"daniel",Guid.NewGuid(),true,now));
        var done = RacTaskLifecycle.Finish(running,"daniel",running.LeaseId!.Value,true,now);
        Assert.Equal(RacTaskStatus.Completed,done.Status);
        Assert.Throws<InvalidOperationException>(() => RacTaskLifecycle.Start(done,"daniel",now));
    }

    [Fact]
    public void ExpiredTasksHaveBoundedRecoveryAndRejectOldLeases()
    {
        var now = DateTimeOffset.UtcNow;
        var task = RacTaskLifecycle.Create("rebeca","daniel","Research",now);
        for (int i = 0; i < RacTaskLifecycle.MaxAttempts; i++)
        {
            var running = RacTaskLifecycle.Start(task,"daniel",now);
            now = now.AddMinutes(5);
            Assert.Throws<InvalidOperationException>(() => RacTaskLifecycle.Finish(running,"daniel",running.LeaseId!.Value,true,now));
            task = RacTaskLifecycle.RecoverExpired(running,now);
        }
        Assert.Equal(RacTaskStatus.Failed,task.Status);
        Assert.Equal(3,task.Attempts);
    }

    [Fact]
    public void RenewalPreservesIdentityAndRejectsExpiredOrForeignLeases()
    {
        var now=DateTimeOffset.UtcNow;
        var task=RacTaskLifecycle.Start(RacTaskLifecycle.Create("rebeca","gabriel","Research",now),"gabriel",now);
        var lease=task.LeaseId!.Value;
        var renewed=RacTaskLifecycle.Renew(task,"gabriel",lease,now.AddMinutes(4));
        Assert.Equal(task with {LeaseUntilUtc=now.AddMinutes(9)},renewed);
        Assert.Throws<InvalidOperationException>(()=>RacTaskLifecycle.Renew(task,"daniel",lease,now));
        Assert.Throws<InvalidOperationException>(()=>RacTaskLifecycle.Renew(task,"gabriel",Guid.NewGuid(),now));
        Assert.Throws<InvalidOperationException>(()=>RacTaskLifecycle.Renew(task,"gabriel",lease,now.AddMinutes(5)));
        Assert.Equal(RacTaskStatus.Completed,RacTaskLifecycle.Finish(renewed,"gabriel",lease,true,now.AddMinutes(8)).Status);
    }
}
