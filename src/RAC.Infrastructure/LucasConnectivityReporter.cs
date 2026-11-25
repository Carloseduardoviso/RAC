using RAC.Application;
namespace RAC.Infrastructure;
public sealed class LucasConnectivityReporter(RacAgentMessageStore messages)
{
    public Task<bool> PublishAsync(Guid taskId,LucasConnectivitySnapshot snapshot,CancellationToken ct)
        =>messages.SaveAsync(LucasConnectivityReport.Create(taskId,snapshot),ct);
}
