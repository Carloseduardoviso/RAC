using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using System.Text.Json;
namespace RAC.Infrastructure;

public sealed class IndependentResearchFreezeStore(IDbContextFactory<RacDbContext> factory,TimeProvider clock)
{
    public async Task<string> SaveAsync(ResearchExperimentPlan.Row row,string manifestId,CancellationToken ct)
    {
        for(var attempt=0;;attempt++)
        {
            try { return await SaveOnceAsync(row,manifestId,ct); }
            catch(Exception ex) when(attempt<3 && IsRetryable(ex)) { await Task.Delay(50*(attempt+1),ct); }
        }
    }
    public async Task<IReadOnlyList<string>> SavePairAsync(ResearchExperimentPlan.Row stress,string manifestId,CancellationToken ct)
    {
        if(stress.Parameters.SlippagePctPerSide!=.20m)throw new ArgumentException("Stress configuration required.",nameof(stress));
        var normal=stress with {Parameters=stress.Parameters with {SlippagePctPerSide=.05m}};
        for(var attempt=0;;attempt++)
        {
            try
            {
                await using var db=await factory.CreateDbContextAsync(ct);
                await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
                var now=clock.GetUtcNow();
                var first=await StageAsync(db,stress,manifestId,now,ct);
                var second=await StageAsync(db,normal,manifestId,now,ct);
                if(clock.GetUtcNow()>=IndependentResearchFreeze.ValidationStart)throw new InvalidOperationException("Freeze window closed.");
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return new[]{first,second};
            }
            catch(Exception ex) when(attempt<3 && IsRetryable(ex)) { await Task.Delay(50*(attempt+1),ct); }
        }
    }
    // Read-only audit: revalidate both historical evidence sets and persisted identities.
    public async Task<IReadOnlyList<string>> AuditPairAsync(ResearchExperimentPlan.Row stress,string manifestId,CancellationToken ct)
    {
        if(stress.Parameters.SlippagePctPerSide!=.20m)throw new ArgumentException("Stress configuration required.",nameof(stress));
        var normal=stress with {Parameters=stress.Parameters with {SlippagePctPerSide=.05m}};
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var ids=new List<string>();
        foreach(var row in new[]{stress,normal})
        {
            var configuration=ResearchExperimentIdentity.Create(row);
            var existing=await db.IndependentResearchFreezes.AsNoTracking().SingleOrDefaultAsync(x=>x.ConfigurationId==configuration,ct)
                ?? throw new InvalidOperationException("Missing frozen configuration.");
            if(existing.FrozenUtc>clock.GetUtcNow())throw new InvalidOperationException("Future freeze timestamp.");
            ids.Add(await StageAsync(db,row,manifestId,existing.FrozenUtc,ct));
        }
        if(db.ChangeTracker.HasChanges())throw new InvalidOperationException("Audit must not modify evidence.");
        await tx.CommitAsync(ct);
        return ids.AsReadOnly();
    }
    private static bool IsRetryable(Exception ex)=>ex is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 1205 or 2601 or 2627 || ex.InnerException is not null && IsRetryable(ex.InnerException);
    private async Task<string> SaveOnceAsync(ResearchExperimentPlan.Row row,string manifestId,CancellationToken ct)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var id=await StageAsync(db,row,manifestId,clock.GetUtcNow(),ct);
        if(clock.GetUtcNow()>=IndependentResearchFreeze.ValidationStart)throw new InvalidOperationException("Freeze window closed.");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return id;
    }
    private async Task<string> StageAsync(RacDbContext db,ResearchExperimentPlan.Row row,string manifestId,DateTimeOffset now,CancellationToken ct)
    {
        var configuration=ResearchExperimentIdentity.Create(row);
        var engine=ResearchEngineCatalog.Current.Id;
        var id=IndependentResearchFreeze.Create(row,engine,manifestId,now);
        manifestId=manifestId.ToUpperInvariant();
        var manifest=await db.ResearchExecutionManifests.SingleAsync(x=>x.ManifestId==manifestId,ct);
        if(manifest.EngineVersion!=engine || manifest.RecordedUtc>now)
            throw new InvalidOperationException("Invalid historical manifest.");
        using var document=JsonDocument.Parse(manifest.EvidenceJson);
        if(document.RootElement.GetProperty("Schema").GetString()!="RAC_MANIFEST_RESULTS_V2")
            throw new InvalidOperationException("Version two manifest required.");
        var hashes=document.RootElement.GetProperty("Results").EnumerateObject().ToDictionary(x=>x.Name,x=>x.Value.GetString(),StringComparer.Ordinal);
        var canonical="RAC:MANIFEST:V1|"+engine+"|"+string.Join("|",hashes.Keys.OrderBy(x=>x,StringComparer.Ordinal));
        var computed=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical)));
        if(computed!=manifestId || hashes.Any(x=>x.Key.Length!=64 || !x.Key.All(Uri.IsHexDigit) || x.Value is null || x.Value.Length!=64 || !x.Value.All(Uri.IsHexDigit)))
            throw new InvalidOperationException("Invalid manifest identity or digest.");
        if(hashes.Count!=manifest.Executions)
            throw new InvalidOperationException("Incomplete manifest.");
        var records=await db.ResearchQuantitativeExecutions.AsNoTracking().Where(x=>x.ConfigurationId==configuration && x.EngineVersion==engine).ToListAsync(ct);
        for(var month=5;month<=10;month++)
        {
            var date=new DateOnly(2026,month,1);
            var matches=records.Where(x=>x.Month==date && hashes.ContainsKey(x.ExecutionId)).ToArray();
            if(matches.Length!=1)throw new InvalidOperationException("Missing or ambiguous historical evidence.");
            var record=matches[0];
            var result=JsonSerializer.Deserialize<ResearchExperimentReplay.Result>(record.ResultJson) ?? throw new InvalidOperationException("Missing result.");
            if(record.ExecutionId!=ResearchExperimentExecutionIdentity.Create(row,record.SourceSha256,date,engine) ||
                record.Symbol!=row.Symbol || JsonSerializer.Deserialize<ResearchBacktestParameters.Experiment>(record.ParametersJson)!=row.Parameters ||
                hashes[record.ExecutionId]!=ResearchResultDigest.Create(record.ExecutionId,result))
                throw new InvalidOperationException("Conflicting historical evidence.");
        }
        var existing=await db.IndependentResearchFreezes.SingleOrDefaultAsync(x=>x.ConfigurationId==configuration,ct);
        if(existing is not null)
        {
            if(existing.ManifestId!=manifestId || existing.EngineVersion!=engine || existing.Symbol!=row.Symbol ||
                JsonSerializer.Deserialize<ResearchBacktestParameters.Experiment>(existing.ParametersJson)!=row.Parameters ||
                existing.FreezeId!=IndependentResearchFreeze.Create(row,engine,manifestId,existing.FrozenUtc))
                throw new InvalidOperationException("Conflicting frozen configuration.");
            return existing.FreezeId;
        }
        if(clock.GetUtcNow()>=IndependentResearchFreeze.ValidationStart)
            throw new InvalidOperationException("Freeze window closed.");
        db.IndependentResearchFreezes.Add(new IndependentResearchFreezeRecord{FreezeId=id,ConfigurationId=configuration,ManifestId=manifestId,EngineVersion=engine,Symbol=row.Symbol,ParametersJson=JsonSerializer.Serialize(row.Parameters),FrozenUtc=now});
        return id;
    }
}
