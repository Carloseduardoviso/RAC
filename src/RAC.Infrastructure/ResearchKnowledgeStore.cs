using Microsoft.EntityFrameworkCore;
using RAC.Domain;
namespace RAC.Infrastructure;
public sealed class ResearchKnowledgeStore(IDbContextFactory<RacDbContext> factory)
{
    public async Task<int> ExtractAsync(DateTimeOffset now,CancellationToken ct)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var publications=await db.ResearchPublications.AsNoTracking().OrderBy(x=>x.Doi).ToListAsync(ct);
        var existing=await db.ResearchKnowledge.AsNoTracking().Where(x=>x.ExtractorVersion=="1").Select(x=>new{x.Doi,x.Category,x.Term}).ToListAsync(ct);
        var keys=existing.Select(x=>(x.Doi,x.Category,x.Term)).ToHashSet();
        int added=0;
        foreach(var p in publications)
        foreach(var hint in ResearchTitleEvidence.Extract(p.Title))
        {
            if(!keys.Add((p.Doi,hint.Category,hint.Term)))continue;
            db.ResearchKnowledge.Add(new ResearchKnowledgeRecord{Doi=p.Doi,Category=hint.Category,Term=hint.Term,Evidence=hint.Evidence,EvidenceLevel="TITLE_MENTION",ExtractorVersion="1",ExtractedUtc=now});
            added++;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return added;
    }
}
