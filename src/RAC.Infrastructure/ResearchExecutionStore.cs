using Microsoft.EntityFrameworkCore;
using RAC.Domain;
namespace RAC.Infrastructure;
public sealed class ResearchExecutionStore(IDbContextFactory<RacDbContext> factory)
{
    public async Task SaveAsync(ResearchExecutionRecord record,CancellationToken ct)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        db.ResearchExecutionHistory.Add(record);
        await db.SaveChangesAsync(ct);
    }
}
