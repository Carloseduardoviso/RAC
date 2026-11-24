using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using System.Text.Json;
namespace RAC.Infrastructure;
public sealed class ResearchPublicationStore(IDbContextFactory<RacDbContext> factory)
{
    public async Task<int> SaveAsync(IReadOnlyList<CrossrefPublicationMetadata.Publication> publications,DateTimeOffset now,CancellationToken ct)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        int added=0;
        foreach(var row in publications.Take(20).DistinctBy(x=>x.Doi,StringComparer.OrdinalIgnoreCase))
        {
            var doi=row.Doi.ToLowerInvariant();
            if(await db.ResearchPublications.AnyAsync(x=>x.Doi==doi,ct))continue;
            db.ResearchPublications.Add(new ResearchPublicationRecord{Doi=doi,Title=row.Title,AuthorsJson=JsonSerializer.Serialize(row.Authors),PublicationYear=row.Year,DiscoveredUtc=now});
            added++;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return added;
    }
}
