using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
namespace RAC.Infrastructure;
public sealed class ResearchSourceStore(IDbContextFactory<RacDbContext> factory)
{
    public async Task LoadAsync(ResearchSourceCandidates candidates,CancellationToken ct)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        var rows=await db.ResearchSources.AsNoTracking().OrderBy(x=>x.DiscoveredUtc).ThenBy(x=>x.Host).Take(1000).ToListAsync(ct);
        foreach(var row in rows) candidates.Observe(row.EvidenceUrl,row.DiscoveredFrom,row.DiscoveredUtc);
    }
    public async Task SaveAsync(ResearchSourceCandidates candidates,CancellationToken ct)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var known=(await db.ResearchSources.Select(x=>x.Host).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach(var row in candidates.Snapshot())
        {
            if(row.Host.Length>253 || row.EvidenceUrl.Length>2048 || known.Count>=1000 || !known.Add(row.Host))continue;
            db.ResearchSources.Add(new ResearchSourceRecord {Host=row.Host,EvidenceUrl=row.EvidenceUrl,DiscoveredFrom=row.DiscoveredFrom,DiscoveredUtc=row.DiscoveredUtc});
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
