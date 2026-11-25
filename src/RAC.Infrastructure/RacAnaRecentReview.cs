using Microsoft.EntityFrameworkCore;
using RAC.Application;

namespace RAC.Infrastructure;

// Single read-only query; reject overflow instead of silently dropping risk notices.
public sealed class RacAnaRecentReview(IDbContextFactory<RacDbContext> factory)
{
    public async Task<RacAnaAdvisoryResult> ReviewAsync(DateTimeOffset now,
        TimeSpan maxAge, CancellationToken ct)
    {
        if(maxAge <= TimeSpan.Zero || maxAge > TimeSpan.FromDays(1))
            throw new ArgumentException("Invalid review window.");
        var since = now - maxAge;
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.AgentMessages.AsNoTracking()
            .Where(x => x.Recipient == "ana" && x.CreatedUtc >= since && x.CreatedUtc <= now &&
                (x.Kind == "Report" || x.Kind == "RiskNotice") &&
                (x.Sender == "rebeca" || x.Sender == "carlos" || x.Sender == "lucas"))
            .OrderBy(x => x.CreatedUtc).ThenBy(x => x.Id).Take(501).ToListAsync(ct);
        if(rows.Count > 500) throw new InvalidOperationException("Review evidence overflow.");
        if(rows.Count == 0) return RacAnaAdvisory.Summarize([]);
        var evidence = rows.Select(x => new RacAgentMessage(x.Id,x.CorrelationId,x.Sender,x.Recipient,
            x.Kind == "Report" ? RacMessageKind.Report : RacMessageKind.RiskNotice,x.Content,x.CreatedUtc));
        return RacAnaAdvisory.Summarize(RacAnaEvidenceBatch.Review(
            rows.Select(x => x.CorrelationId), evidence, now, maxAge));
    }
}
