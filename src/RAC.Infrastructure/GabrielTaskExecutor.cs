using RAC.Application;
using System.Security.Cryptography;
using System.Text;
namespace RAC.Infrastructure;

public sealed class GabrielTaskExecutor(RacAgentTaskStore tasks,RacAgentMessageStore messages,
    GabrielResearchReporter reporter,Func<CancellationToken,Task<string>> readMatrix)
{
    public async Task<bool> ExecuteNextAsync(CancellationToken ct)
    {
        var task=await tasks.ClaimAsync("gabriel",ct);
        if(task is null) return false;
        var lease=task.LeaseId!.Value;
        if(task.Description!="QUANTITATIVE_RESEARCH" || task.Leader!="rebeca")
        {
            await tasks.FinishAsync(task.Id,"gabriel",lease,false,ct);
            return true;
        }
        var existing=await messages.ReadInboxAsync("rebeca",task.Id,10,ct);
        if(!existing.Any(x=>x.Sender=="gabriel" && x.Kind=="Report"))
        {
            var json=await readMatrix(ct);
            if(json.Length>8*1024*1024) throw new InvalidDataException("Matrix too large.");
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
            await reporter.PublishAsync(task.Id,task.UpdatedUtc,hash,json,ct);
        }
        await tasks.FinishAsync(task.Id,"gabriel",lease,true,ct);
        return true;
    }
}
