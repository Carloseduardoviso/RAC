using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using System.Text.Json;
namespace RAC.Infrastructure;

// Research-only persistence; caller must independently authenticate the archive source.
public sealed class IndependentResearchEvidenceStore(IDbContextFactory<RacDbContext> factory,IndependentResearchFreezeStore freezes,TimeProvider clock)
{
    public async Task<string> SaveAsync(ResearchExperimentPlan.Row row,DateOnly month,byte[] archive,string sha256,CancellationToken ct)
    {
        var configuration=ResearchExperimentIdentity.Create(row);
        await using var db=await factory.CreateDbContextAsync(ct);
        var frozen=await db.IndependentResearchFreezes.AsNoTracking().SingleAsync(x=>x.ConfigurationId==configuration,ct);
        var stress=row with {Parameters=row.Parameters with {SlippagePctPerSide=.20m}};
        var audited=await freezes.AuditPairAsync(stress,frozen.ManifestId,ct);
        if(!audited.Contains(frozen.FreezeId,StringComparer.Ordinal))throw new InvalidOperationException("Freeze audit mismatch.");
        var now=clock.GetUtcNow();
        var record=IndependentResearchArchiveEvidence.Replay(row,month,archive,sha256,frozen.ManifestId,frozen.FrozenUtc,now,ct);
        if(record.FreezeId!=frozen.FreezeId || !IndependentResearchArchiveEvidence.Verify(record,row,frozen.ManifestId,frozen.FrozenUtc))
            throw new InvalidOperationException("Invalid independent evidence.");
        for(var attempt=0;;attempt++)
        {
            try
            {
                await using var write=await factory.CreateDbContextAsync(ct);
                await using var tx=await write.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
                var existing=await write.IndependentResearchEvidence.AsNoTracking().SingleOrDefaultAsync(x=>x.FreezeId==record.FreezeId && x.Month==month,ct);
                if(existing is not null)
                {
                    if(existing.ExecutionId!=record.ExecutionId || existing.SourceSha256!=record.SourceSha256 || existing.Digest!=record.Digest ||
                        JsonSerializer.Deserialize<IndependentResearchGate.Evidence>(existing.EvidenceJson)!=record.Evidence)
                        throw new InvalidOperationException("Conflicting independent evidence.");
                }
                else
                {
                    write.IndependentResearchEvidence.Add(new IndependentResearchEvidenceRecord
                    {
                        ExecutionId=record.ExecutionId,FreezeId=record.FreezeId,SourceSha256=record.SourceSha256,
                        Digest=record.Digest,Month=month,EvidenceJson=JsonSerializer.Serialize(record.Evidence),RecordedUtc=now
                    });
                    await write.SaveChangesAsync(ct);
                }
                await tx.CommitAsync(ct);
                return record.ExecutionId;
            }
            catch(Exception ex) when(attempt<3 && Retryable(ex)){await Task.Delay(50*(attempt+1),ct);}
        }
    }
    private static bool Retryable(Exception ex)=>ex is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 1205 or 2601 or 2627 || ex.InnerException is not null && Retryable(ex.InnerException);
}
