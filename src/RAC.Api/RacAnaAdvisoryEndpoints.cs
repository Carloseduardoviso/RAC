using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using RAC.Infrastructure;
using System.Text.Json;

namespace RAC.Api;

public sealed record RebecaMarketReviewRequest(Guid DanielTaskId,Guid GabrielTaskId);

public static class RacAnaAdvisoryEndpoints
{
    public static void MapRacAnaAdvisories(this WebApplication app)
    {
        app.MapPost("/api/ana/rebeca/market-review", async ([FromBody] RebecaMarketReviewRequest request,[FromServices] RebecaMarketAdvisoryPublisher publisher,CancellationToken ct) =>
        {
            if(request is null || request.DanielTaskId==Guid.Empty || request.GabrielTaskId==Guid.Empty ||
                request.DanielTaskId==request.GabrielTaskId)
                return Results.BadRequest(new {error="Invalid specialist task pair."});
            try
            {
                var result=await publisher.ReviewAndPublishAsync(request.DanielTaskId,request.GabrielTaskId,
                    DateTimeOffset.UtcNow,TimeSpan.FromMinutes(15),ct);
                return Results.Ok(new {result.Review.Status,result.Review.Reasons,result.Review.EvidenceIds,
                    result.CorrelationId,result.MessageId,result.Published,result.TradingAuthorized});
            }
            catch(ArgumentException ex){return Results.BadRequest(new {error=ex.Message});}
            catch(InvalidOperationException ex){return Results.Conflict(new {error=ex.Message});}
        }).RequireAuthorization("RacRiskReviewer");
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
