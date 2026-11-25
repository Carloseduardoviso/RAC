using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using System.Text.Json;
namespace RAC.Infrastructure;
public sealed class ResearchExecutionManifestStore(IDbContextFactory<RacDbContext> factory)
{
    public async Task<bool> SaveAsync(IReadOnlyList<string> symbols,string engineVersion,IReadOnlyList<ResearchExecutionManifest.Evidence> evidence,DateTimeOffset now,CancellationToken ct)
    {
        var manifest=ResearchExecutionManifest.Create(symbols,engineVersion,evidence);
        var ids=evidence.Select(x=>ResearchExperimentExecutionIdentity.Create(x.Row,x.SourceSha256,x.Month,engineVersion)).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        var payload=JsonSerializer.Serialize(ids);
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        foreach(var batch in ids.Chunk(500))
        {
            var found=await db.ResearchQuantitativeExecutions.Where(x=>batch.Contains(x.ExecutionId)&&x.EngineVersion==engineVersion).Select(x=>x.ExecutionId).ToListAsync(ct);
            if(!found.ToHashSet(StringComparer.Ordinal).SetEquals(batch))
                throw new InvalidOperationException("Missing persisted research executions.");
        }
        var existing=await db.ResearchExecutionManifests.SingleOrDefaultAsync(x=>x.ManifestId==manifest.Id,ct);
        if(existing is not null)
        {
            if(existing.EngineVersion!=engineVersion||existing.Assets!=manifest.Assets||existing.Executions!=manifest.Executions||existing.EvidenceJson!=payload)
                throw new InvalidOperationException("Conflicting research manifest.");
            return false;
        }
        db.ResearchExecutionManifests.Add(new ResearchExecutionManifestRecord{ManifestId=manifest.Id,EngineVersion=engineVersion,Assets=manifest.Assets,Executions=manifest.Executions,EvidenceJson=payload,RecordedUtc=now.ToUniversalTime()});
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }
}
