using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using System.Text.Json;
namespace RAC.Infrastructure;
public sealed class ResearchExecutionManifestStore(IDbContextFactory<RacDbContext> factory)
{
    public async Task<bool> SaveAsync(IReadOnlyList<string> symbols,string engineVersion,IReadOnlyList<ResearchExecutionManifest.Evidence> evidence,DateTimeOffset now,CancellationToken ct)
    {
        for(var attempt=0;;attempt++)
        {
            try { return await SaveOnceAsync(symbols,engineVersion,evidence,now,ct); }
            catch(Exception ex) when(attempt<3 && IsRetryable(ex)) { await Task.Delay(50*(attempt+1),ct); }
        }
    }
    private static bool IsRetryable(Exception ex)=>ex is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 1205 or 2601 or 2627 || ex.InnerException is not null && IsRetryable(ex.InnerException);
    private async Task<bool> SaveOnceAsync(IReadOnlyList<string> symbols,string engineVersion,IReadOnlyList<ResearchExecutionManifest.Evidence> evidence,DateTimeOffset now,CancellationToken ct)
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
