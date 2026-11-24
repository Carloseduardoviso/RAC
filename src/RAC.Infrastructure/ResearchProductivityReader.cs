using Microsoft.EntityFrameworkCore;
namespace RAC.Infrastructure;
public sealed class ResearchProductivityReader(IDbContextFactory<RacDbContext> factory)
{
    public async Task<IReadOnlyList<ResearchProductivity.Metric>> ReadAsync(DateTimeOffset now,CancellationToken ct)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        var cutoff=now.AddDays(-30);
        var rows=await db.ResearchExecutionHistory.AsNoTracking().Where(x=>x.StartedUtc>=cutoff&&x.StartedUtc<=now)
            .OrderByDescending(x=>x.StartedUtc).ThenByDescending(x=>x.Id).Take(1000).ToListAsync(ct);
        return ResearchProductivity.Summarize(rows);
    }
}
