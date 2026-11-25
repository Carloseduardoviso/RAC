namespace RAC.Application;

public enum RacTaskStatus { Pending, Running, Completed, Failed }

public sealed record RacAgentTask(Guid Id, string Leader, string Specialist, string Description,
    RacTaskStatus Status, int Attempts, DateTimeOffset UpdatedUtc, Guid? LeaseId, DateTimeOffset? LeaseUntilUtc);

public static class RacTaskLifecycle
{
    public const int MaxAttempts = 3;

    public static RacAgentTask Create(string leader, string specialist, string description, DateTimeOffset now)
    {
        if (!RacOrganization.Specialists.Any(x => x.Id == specialist))
            throw new ArgumentException("Specialist required.");
        var message = RacAgentCommunication.Create(leader, specialist, RacMessageKind.TaskRequest,
            description, Guid.NewGuid(), now);
        return new(message.Id, leader, specialist, description, RacTaskStatus.Pending, 0, now.ToUniversalTime(), null, null);
    }

    public static RacAgentTask Start(RacAgentTask task, string specialist, DateTimeOffset now)
    {
        if (task.Specialist != specialist || task.Status != RacTaskStatus.Pending ||
            task.Attempts >= MaxAttempts || now < task.UpdatedUtc)
            throw new InvalidOperationException("Task cannot be started.");
        return task with { Status = RacTaskStatus.Running, Attempts = task.Attempts + 1,
            UpdatedUtc = now.ToUniversalTime(), LeaseId = Guid.NewGuid(), LeaseUntilUtc = now.ToUniversalTime().AddMinutes(5) };
    }

    public static RacAgentTask Finish(RacAgentTask task, string specialist, Guid lease, bool success, DateTimeOffset now)
    {
        if (task.Specialist != specialist || task.Status != RacTaskStatus.Running ||
            task.LeaseId != lease || task.LeaseUntilUtc is null || now >= task.LeaseUntilUtc || now < task.UpdatedUtc)
            throw new InvalidOperationException("Invalid or expired task lease.");
        return task with { Status = success ? RacTaskStatus.Completed : RacTaskStatus.Failed,
            UpdatedUtc = now.ToUniversalTime(), LeaseId = null, LeaseUntilUtc = null };
    }

    public static RacAgentTask RecoverExpired(RacAgentTask task, DateTimeOffset now)
    {
        if (task.Status != RacTaskStatus.Running || task.LeaseUntilUtc is null || now < task.LeaseUntilUtc)
            throw new InvalidOperationException("Task has no expired lease.");
        return task with { Status = task.Attempts >= MaxAttempts ? RacTaskStatus.Failed : RacTaskStatus.Pending,
            UpdatedUtc = now.ToUniversalTime(), LeaseId = null, LeaseUntilUtc = null };
    }
}
