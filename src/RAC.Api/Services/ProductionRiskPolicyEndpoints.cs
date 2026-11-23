using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Api.Services;

public sealed record ProductionPolicyProposalRequest(
    decimal MaximumCapitalUsdt, decimal MaximumOrderUsdt,
    decimal MaximumDailyLossPercent, decimal MaximumDrawdownPercent,
    int MaximumOpenPositions, int MaximumOrdersPerHour, int MaximumOrdersPerDay,
    string[] AllowedSymbols);

public sealed record ProductionPolicyReviewRequest(
    string Reason, bool Approve);

public static class ProductionRiskPolicyEndpoints
{
    public static IEndpointRouteBuilder MapProductionRiskPolicyEndpoints(
        this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/risk/policies", async (
            ProductionRiskPolicyStore store, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            try { return Results.Ok(await store.HistoryAsync(ct)); }
            catch (InvalidDataException)
            {
                return Results.Conflict(new { error =
                    "Policy or audit integrity failure. Review blocked." });
            }
            catch (Exception ex) when (ex is DbUpdateException or
                Microsoft.Data.SqlClient.SqlException)
            {
                return Results.Json(new { error = "Risk policy SQL unavailable." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).RequireAuthorization("RacRiskReviewer");

        app.MapPost("/api/risk/policies", async (
            ProductionPolicyProposalRequest request, ClaimsPrincipal user,
            ProductionRiskPolicyStore store, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (request is null || request.AllowedSymbols is null ||
                request.AllowedSymbols.Length is < 1 or > 12 ||
                request.AllowedSymbols.Any(s => string.IsNullOrWhiteSpace(s) ||
                    !RacMarketSymbols.IsSupported(s)) ||
                request.AllowedSymbols.Distinct(StringComparer.Ordinal).Count() !=
                    request.AllowedSymbols.Length)
                return Results.BadRequest(new { error = "Invalid symbol allowlist." });
            try
            {
                var limits = new ProductionRiskLimits(
                    request.MaximumCapitalUsdt, request.MaximumOrderUsdt,
                    request.MaximumDailyLossPercent, request.MaximumDrawdownPercent,
                    request.MaximumOpenPositions, request.MaximumOrdersPerHour,
                    request.MaximumOrdersPerDay,
                    new HashSet<string>(request.AllowedSymbols, StringComparer.Ordinal));
                var proposal = await store.ProposeAsync(limits,
                    user.Identity?.Name ?? "", ct);
                return Results.Ok(new { proposal.Id, proposal.Version,
                    proposal.Status, orderPlacementEnabled = false });
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
            {
                return Results.BadRequest(new { error = "Unapproved or invalid risk limits." });
            }
            catch (DbUpdateException)
            {
                return Results.Conflict(new { error = "Policy version conflict." });
            }
            catch (Microsoft.Data.SqlClient.SqlException)
            {
                return Results.Json(new { error = "Risk policy SQL unavailable or locked." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).RequireAuthorization("RacRiskReviewer");

        // Role is resolved from authenticated claims, never from request input.
        app.MapPost("/api/risk/policies/{id:long}/review", async (
            long id, ProductionPolicyReviewRequest request, ClaimsPrincipal user,
            ProductionRiskPolicyStore store, HttpContext http, CancellationToken ct) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (id <= 0 || request is null ||
                string.IsNullOrWhiteSpace(request.Reason) ||
                request.Reason.Length > 500)
                return Results.BadRequest(new { error = "Invalid review request." });
            try
            {
                var result = await store.ReviewAsync(id, user.Identity?.Name ?? "",
                    user.FindFirstValue(ClaimTypes.Role) ?? "", request.Reason,
                    request.Approve, ct);
                return Results.Ok(new { result.Id, result.Version,
                    result.Status, orderPlacementEnabled = false });
            }
            catch (InvalidOperationException)
            {
                return Results.Conflict(new { error = "Independent administrator review required." });
            }
            catch (InvalidDataException)
            {
                return Results.Conflict(new { error = "Policy already reviewed or integrity check failed." });
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Conflict(new { error = "Policy review conflict." });
            }
            catch (Microsoft.Data.SqlClient.SqlException)
            {
                return Results.Json(new { error = "Risk policy SQL unavailable or locked." },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }).RequireAuthorization("RacWalletOwner");
        return app;
    }
}
