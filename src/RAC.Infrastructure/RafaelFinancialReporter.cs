using RAC.Application;
namespace RAC.Infrastructure;

public sealed class RafaelFinancialReporter(RacAgentMessageStore messages)
{
    public RacAgentMessage Create(Guid taskId,DateTimeOffset observedUtc,RafaelStoredCapital? data,RafaelAllocationLimits limits,TimeSpan maximumAge,decimal tolerance) => RafaelFinancialReport.Create(taskId,observedUtc,data,limits,maximumAge,tolerance);
    public Task<bool> PublishAsync(Guid taskId,DateTimeOffset observedUtc,RafaelStoredCapital? data,
        RafaelAllocationLimits limits,TimeSpan maximumAge,decimal tolerance,CancellationToken ct)
        =>messages.SaveAsync(RafaelFinancialReport.Create(taskId,observedUtc,data,limits,maximumAge,tolerance),ct);
}
