using Microsoft.EntityFrameworkCore;
using RAC.Application;

namespace RAC.Infrastructure;

public sealed class DanielTaskExecutor(
    IDbContextFactory<RacDbContext> factory,
    DanielBinancePublicClient market,
    RacAgentTaskStore tasks,
    DanielDiscoveryStore discoveries,
    DanielDiscoveryReporter reporter)
{
    public async Task<bool> ExecuteNextAsync(CancellationToken ct)
    {
        var task=await tasks.ClaimAsync("daniel",ct);
        if(task is null) return false;
        var lease=task.LeaseId!.Value;
        if(task.Description!="MARKET_DISCOVERY")
        {
            await tasks.FinishAsync(task.Id,"daniel",lease,false,ct);
            return true;
        }
        await using(var db=await factory.CreateDbContextAsync(ct))
        {
            if(!await db.DanielDiscoveries.AnyAsync(x=>x.TaskId==task.Id,ct))
            {
                var observations=await market.ReadAsync(ct);
                if(observations.Count==0) throw new InvalidOperationException("No valid market observations.");
                await discoveries.SaveAsync(task.Id,lease,observations,DateTimeOffset.UtcNow,ct);
            }
        }
        await reporter.PublishAsync(task.Id,ct);
        await tasks.FinishAsync(task.Id,"daniel",lease,true,ct);
        return true;
    }
}
