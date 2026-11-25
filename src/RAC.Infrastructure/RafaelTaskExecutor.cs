using RAC.Application;
namespace RAC.Infrastructure;

public sealed class RafaelTaskExecutor(RacAgentTaskStore tasks,RacAgentMessageStore messages,
    RafaelFinancialReporter reporter,RafaelCapitalReader reader,RafaelAllocationLimits limits,
    TimeSpan maximumAge,decimal tolerance)
{
    public async Task<bool> ExecuteNextAsync(CancellationToken ct)
    {
        var task=await tasks.ClaimAsync("rafael",ct);
        if(task is null) return false;
        var lease=task.LeaseId!.Value;
        if(task.Description!="CAPITAL_ANALYSIS" || task.Leader!="carlos")
        {
            await tasks.FinishAsync(task.Id,"rafael",lease,false,ct);
            return true;
        }
        var existing=await messages.ReadInboxAsync("carlos",task.Id,10,ct);
        if(!existing.Any(x=>x.Sender=="rafael" && x.Kind=="Report"))
        {
            var data=await reader.ReadAsync(ct);
            await reporter.PublishAsync(task.Id,DateTimeOffset.UtcNow,data,limits,maximumAge,tolerance,ct);
        }
        await tasks.FinishAsync(task.Id,"rafael",lease,true,ct);
        return true;
    }
}
