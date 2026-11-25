using RAC.Application;
namespace RAC.Infrastructure;
public sealed class LucasTaskExecutor(RacAgentTaskStore tasks,RacAgentMessageStore messages,
    LucasConnectivityReporter reporter,Func<CancellationToken,Task<LucasConnectivitySnapshot>> assess)
{
    public async Task<bool> ExecuteNextAsync(CancellationToken ct)
    {
        var task=await tasks.ClaimAsync("lucas",ct);
        if(task is null) return false;
        var lease=task.LeaseId!.Value;
        if(task.Description!="EXCHANGE_HEALTH" || task.Leader!="ana")
        {
            await tasks.FinishAsync(task.Id,"lucas",lease,false,ct);
            return true;
        }
        var existing=await messages.ReadInboxAsync("ana",task.Id,10,ct);
        if(!existing.Any(x=>x.Sender=="lucas" && x.Kind=="Report"))
        {
            var snapshot=await assess(ct);
            await reporter.PublishAsync(task.Id,snapshot,ct);
        }
        await tasks.FinishAsync(task.Id,"lucas",lease,true,ct);
        return true;
    }
}
