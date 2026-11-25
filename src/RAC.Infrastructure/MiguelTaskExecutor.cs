namespace RAC.Infrastructure;
public sealed class MiguelTaskExecutor(RacAgentTaskStore tasks,RacAgentMessageStore messages,
    MiguelAuditReporter reporter,MiguelAuditReader reader,decimal moneyTolerance,
    decimal quantityTolerance,TimeSpan maximumAge)
{
    public async Task<bool> ExecuteNextAsync(CancellationToken ct)
    {
        var task=await tasks.ClaimAsync("miguel",ct);
        if(task is null) return false;
        var lease=task.LeaseId!.Value;
        if(task.Description!="AUDIT" || task.Leader!="carlos")
        {
            await tasks.FinishAsync(task.Id,"miguel",lease,false,ct);
            return true;
        }
        var existing=await messages.ReadInboxAsync("carlos",task.Id,10,ct);
        if(!existing.Any(x=>x.Sender=="miguel" && x.Kind=="Report"))
        {
            var data=await reader.ReadAsync(moneyTolerance,quantityTolerance,ct);
            await tasks.CompleteWithReportAsync(task.Id,"miguel",lease,reporter.Create(task.Id,data,DateTimeOffset.UtcNow,maximumAge),ct);
            return true;
        }
        await tasks.CompleteWithReportAsync(task.Id,"miguel",lease,null,ct);
        return true;
    }
}
