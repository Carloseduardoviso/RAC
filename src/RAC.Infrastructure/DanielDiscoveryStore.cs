using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using System.Text.Json;

namespace RAC.Infrastructure;

public sealed class DanielDiscoveryStore(IDbContextFactory<RacDbContext> factory)
{
    public async Task<bool> SaveAsync(Guid taskId,Guid leaseId,IReadOnlyList<DanielMarketObservation> observations,DateTimeOffset observedUtc,CancellationToken ct)
    {
        var now=DateTimeOffset.UtcNow;
        if(taskId==Guid.Empty || leaseId==Guid.Empty || observedUtc>now || now-observedUtc>TimeSpan.FromMinutes(2))
            throw new ArgumentException("Invalid discovery context.");
        var candidates=DanielCandidateRanking.Rank(observations,observedUtc);
        var json=JsonSerializer.Serialize(candidates);
        var snapshotJson=JsonSerializer.Serialize(DanielResearchSnapshot.Create(observations,observedUtc));
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var task=await db.AgentTasks.SingleAsync(x=>x.Id==taskId,ct);
        if(task.Specialist!="daniel" || task.Leader!="rebeca" || task.Status!="Running" ||
            task.LeaseId!=leaseId || task.LeaseUntilUtc is null || task.LeaseUntilUtc<=DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Invalid discovery lease.");
        var existing=await db.DanielDiscoveries.SingleOrDefaultAsync(x=>x.TaskId==taskId,ct);
        if(existing is not null)
        {
            if(existing.CandidatesJson!=json || existing.ObservedUtc!=observedUtc || (existing.ResearchSnapshotJson is not null && existing.ResearchSnapshotJson!=snapshotJson))
                throw new InvalidOperationException("Discovery already recorded.");
            return false;
        }
        db.DanielDiscoveries.Add(new DanielDiscoveryRecord
        {
            TaskId=taskId,ObservedUtc=observedUtc.ToUniversalTime(),CandidatesJson=json,CandidateCount=candidates.Count,ResearchSnapshotJson=snapshotJson
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<DanielDiscoveryRecord>> ReadRecentAsync(int limit,CancellationToken ct)
    {
        if(limit is <1 or >100) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var db=await factory.CreateDbContextAsync(ct);
        return await db.DanielDiscoveries.AsNoTracking().OrderByDescending(x=>x.ObservedUtc)
            .ThenBy(x=>x.TaskId).Take(limit).ToListAsync(ct);
    }
}
