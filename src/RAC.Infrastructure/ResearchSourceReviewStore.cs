using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using System.Text.Json;
namespace RAC.Infrastructure;
public sealed class ResearchSourceReviewStore(IDbContextFactory<RacDbContext> factory)
{
    public async Task<int> RecordPendingAsync(DateTimeOffset now,CancellationToken ct)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var cutoff=now.AddDays(-7);
        var sources=await db.ResearchSources.AsNoTracking()
            .Where(s=>!db.ResearchSourceReviews.Any(r=>r.Host==s.Host && r.ReviewedUtc>=cutoff))
            .OrderBy(s=>s.DiscoveredUtc).ThenBy(s=>s.Host).Take(100).ToListAsync(ct);
        var evidence=new ResearchSourceReliability.Evidence(null,null,null,null,null,false);
        var assessment=ResearchSourceReliability.Evaluate(evidence);
        foreach(var source in sources)
            db.ResearchSourceReviews.Add(new ResearchSourceReviewRecord
            {
                Host=source.Host,ReviewedUtc=now,Score=assessment.Score,Status=assessment.Status,
                RubricVersion="1",EvidenceJson=JsonSerializer.Serialize(new {source.EvidenceUrl,source.DiscoveredFrom,Evidence=evidence})
            });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return sources.Count;
    }
}
