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
        app.MapGet("/api/ana/research/freeze-audit/{configurationId}", async (string configurationId, IDbContextFactory<RacDbContext> factory, HttpContext context, CancellationToken ct) =>
        {
            if(configurationId.Length!=64 || !configurationId.All(Uri.IsHexDigit))
                return Results.BadRequest(new {error="Invalid configuration identity."});
            var store=context.RequestServices.GetService<IndependentResearchFreezeStore>();
            if(store is null)return Results.StatusCode(503);
            await using var db=await factory.CreateDbContextAsync(ct);
            var frozen=await db.IndependentResearchFreezes.AsNoTracking().SingleOrDefaultAsync(x=>x.ConfigurationId==configurationId.ToUpperInvariant(),ct);
            if(frozen is null)return Results.NotFound();
            try
            {
                var parameters=JsonSerializer.Deserialize<RAC.Infrastructure.ResearchBacktestParameters.Experiment>(frozen.ParametersJson);
                if(parameters is null || parameters.SlippagePctPerSide!=.20m)
                    return Results.BadRequest(new {error="Stress configuration required."});
                var row=new RAC.Application.ResearchExperimentPlan.Row(frozen.Symbol,parameters);
                if(RAC.Application.ResearchExperimentIdentity.Create(row)!=frozen.ConfigurationId)return Results.Conflict(new {error="Research evidence invalid."});
                var ids=await store.AuditPairAsync(row,frozen.ManifestId,ct);
                return Results.Ok(new {Status="HISTORICAL_EVIDENCE_VERIFIED",FreezeIds=ids,frozen.ManifestId,IndependentValidation=false,TradingAuthorized=false});
            }
            catch(ArgumentException){return Results.Conflict(new {error="Research evidence invalid."});}
            catch(InvalidOperationException){return Results.Conflict(new {error="Research evidence invalid."});}
            catch(JsonException){return Results.Conflict(new {error="Research evidence invalid."});}
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
