using RAC.Application;
using System.Security.Cryptography;
using System.Text;
namespace RAC.Infrastructure;

public sealed class GabrielTaskExecutor(RacAgentTaskStore tasks,RacAgentMessageStore messages,
    GabrielResearchReporter reporter,Func<CancellationToken,Task<string>> readMatrix,Func<IReadOnlyList<string>,CancellationToken,Task<string>>? readDynamicMatrix=null)
{
    public async Task<bool> ExecuteNextAsync(CancellationToken ct)
    {
        var task=await tasks.ClaimAsync("gabriel",ct);
        if(task is null) return false;
        var lease=task.LeaseId!.Value;
        var linked=task.Description.StartsWith("QUANTITATIVE_RESEARCH:",StringComparison.Ordinal);
        var discoveryId=Guid.Empty;
        if(task.Leader!="rebeca" || (task.Description!="QUANTITATIVE_RESEARCH" && (!linked || !Guid.TryParseExact(task.Description["QUANTITATIVE_RESEARCH:".Length..],"N",out discoveryId) || discoveryId==Guid.Empty)))
        {
            await tasks.FinishAsync(task.Id,"gabriel",lease,false,ct);
            return true;
        }
        var existing=await messages.ReadInboxAsync("rebeca",task.Id,10,ct);
        if(!existing.Any(x=>x.Sender=="gabriel" && x.Kind=="Report"))
        {
            var evidence=linked?await new GabrielDiscoveryEvidenceReader(new RebecaMarketEvidenceReader(messages)).ReadAsync(discoveryId,DateTimeOffset.UtcNow,ct):null;
            var symbols=evidence is not null && readDynamicMatrix is not null?evidence.Candidates.Select(x=>x.Symbol).ToArray():null;
            var json=symbols is null?await readMatrix(ct):await readDynamicMatrix!(symbols,ct);
            if(json.Length>8*1024*1024) throw new InvalidDataException("Matrix too large.");
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
            var report=reporter.Create(task.Id,task.UpdatedUtc,hash,json,symbols);
            if(evidence is not null)
            {
                var assessments=GabrielDiscoveryResearch.Assess(evidence,GabrielExperimentMatrix.Read(json,symbols));
                var content=System.Text.Json.Nodes.JsonNode.Parse(report.Content)!.AsObject();
                content["DanielTaskId"]=System.Text.Json.Nodes.JsonValue.Create(discoveryId);
                content["DanielMessageId"]=System.Text.Json.Nodes.JsonValue.Create(evidence.MessageId);
                content["DiscoveryAssessments"]=System.Text.Json.JsonSerializer.SerializeToNode(assessments);
                var enriched=content.ToJsonString();
                if(enriched.Length>4096) throw new InvalidDataException("Linked research report too large.");
                report=report with {Content=enriched};
            }
            await tasks.CompleteWithReportAsync(task.Id,"gabriel",lease,report,ct);
            return true;
        }
        await tasks.CompleteWithReportAsync(task.Id,"gabriel",lease,null,ct);
        return true;
    }
}
