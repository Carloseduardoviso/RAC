using Microsoft.EntityFrameworkCore;
using RAC.Infrastructure;
using System.Text.Json;

namespace RAC.Api;

public static class RacAnaAdvisoryEndpoints
{
    public static void MapRacAnaAdvisories(this WebApplication app)
    {
        app.MapGet("/api/ana/advisories", async (int? limit, IDbContextFactory<RacDbContext> factory, CancellationToken ct) =>
        {
            var count=limit ?? 20;
            if(count is < 1 or > 100) return Results.BadRequest(new { error="Invalid limit." });
            await using var db=await factory.CreateDbContextAsync(ct);
            var rows=await db.AnaAdvisories.AsNoTracking().OrderByDescending(x=>x.CreatedUtc)
                .ThenBy(x=>x.Fingerprint).Take(count).ToListAsync(ct);
            return Results.Ok(rows.Select(x=>new {
                x.Fingerprint,x.Status,x.CreatedUtc,
                Reasons=JsonSerializer.Deserialize<string[]>(x.ReasonsJson),
                EvidenceIds=JsonSerializer.Deserialize<Guid[]>(x.EvidenceIdsJson),
                TradingAuthorized=false
            }).ToArray());
        }).RequireAuthorization("RacRiskReviewer");
    }
}
