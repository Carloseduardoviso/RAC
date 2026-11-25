namespace RAC.Infrastructure;
public sealed class MiguelAuditReporter(RacAgentMessageStore messages)
{
    public Task<bool> PublishAsync(Guid taskId,MiguelStoredAudit? data,DateTimeOffset observedUtc,
        TimeSpan maximumAge,CancellationToken ct)
        =>messages.SaveAsync(MiguelAuditReport.Create(taskId,data,observedUtc,maximumAge),ct);
}
