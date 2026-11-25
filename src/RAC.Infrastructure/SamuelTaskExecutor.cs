using RAC.Application;
namespace RAC.Infrastructure;
public sealed class SamuelTaskExecutor(RacAgentTaskStore tasks,RacAgentMessageStore messages,
    SamuelLiquidityReporter reporter,Func<string,CancellationToken,Task<SamuelDepthSnapshot>> read,
    string symbol,IReadOnlyList<decimal> quantities,decimal feePercent,decimal maxImpactPercent,decimal maxCostPercent)
{
    public async Task<bool> ExecuteNextAsync(CancellationToken ct)
    {
        var task=await tasks.ClaimAsync("samuel",ct);
        if(task is null) return false;
        var lease=task.LeaseId!.Value;
        if(task.Description!="LIQUIDITY_COST_ANALYSIS" || task.Leader!="carlos")
        {
            await tasks.FinishAsync(task.Id,"samuel",lease,false,ct);
            return true;
        }
        var existing=await messages.ReadInboxAsync("carlos",task.Id,10,ct);
        if(!existing.Any(x=>x.Sender=="samuel" && x.Kind=="Report"))
        {
            var book=await read(symbol,ct);
            await tasks.CompleteWithReportAsync(task.Id,"samuel",lease,reporter.Create(task.Id,symbol,book,quantities,feePercent,maxImpactPercent,maxCostPercent,DateTimeOffset.UtcNow),ct);
            return true;
        }
        await tasks.CompleteWithReportAsync(task.Id,"samuel",lease,null,ct);
        return true;
    }
}
