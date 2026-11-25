using RAC.Application;
namespace RAC.Infrastructure;
public sealed class LucasConnectivityReporter(RacAgentMessageStore messages)
{
    public RacAgentMessage Create(Guid taskId,LucasConnectivitySnapshot snapshot) => LucasConnectivityReport.Create(taskId,snapshot);
    public Task<bool> PublishAsync(Guid taskId,LucasConnectivitySnapshot snapshot,CancellationToken ct)
        =>messages.SaveAsync(LucasConnectivityReport.Create(taskId,snapshot),ct);
}
