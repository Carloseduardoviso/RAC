using Microsoft.EntityFrameworkCore;
using RAC.Application;
using System.Text.Json;

namespace RAC.Infrastructure;

public sealed class DanielDiscoveryReporter(IDbContextFactory<RacDbContext> factory)
{
    public async Task<RacAgentMessage> CreateAsync(Guid taskId,CancellationToken ct)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        var discovery=await db.DanielDiscoveries.AsNoTracking().SingleAsync(x=>x.TaskId==taskId,ct);
        var candidates=JsonSerializer.Deserialize<DanielRankedCandidate[]>(discovery.CandidatesJson)
            ?? throw new InvalidOperationException("Missing candidates.");
        if(candidates.Length!=discovery.CandidateCount || candidates.Length>20)
            throw new InvalidOperationException("Invalid discovery record.");
        var existing=await db.AgentMessages.AsNoTracking().SingleOrDefaultAsync(x=>x.CorrelationId==taskId && x.Sender=="daniel" && x.Recipient=="rebeca" && x.Kind=="Report",ct);
        var coverageStatus=DanielResearchEvidence.Classify(discovery.ResearchSnapshotJson,discovery.CandidateCount);
        var content=JsonSerializer.Serialize(new { Mode="RESEARCH_ONLY",TradingAuthorized=false,CoverageStatus=coverageStatus,
            discovery.TaskId,discovery.ObservedUtc,Candidates=candidates });
        var message=RacAgentCommunication.Create("daniel","rebeca",RacMessageKind.Report,content,taskId,discovery.ObservedUtc);
        var hash=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("DANIEL:REPORT:V1:"+taskId.ToString("N")));
        var id=new Guid(hash.AsSpan(0,16));
        if(existing is not null)
        {
            var legacy=JsonSerializer.Serialize(new { Mode="RESEARCH_ONLY",TradingAuthorized=false,discovery.TaskId,discovery.ObservedUtc,Candidates=candidates });
            if(existing.Id!=id || existing.CreatedUtc!=discovery.ObservedUtc || (existing.Content!=content && existing.Content!=legacy)) throw new InvalidOperationException("Conflicting discovery report.");
            return message with { Id=id,Content=existing.Content };
        }
        return message with { Id=id };
    }
    public async Task<bool> PublishAsync(Guid taskId,CancellationToken ct) => await new RacAgentMessageStore(factory).SaveAsync(await CreateAsync(taskId,ct),ct);
}
