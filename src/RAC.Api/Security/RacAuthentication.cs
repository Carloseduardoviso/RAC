using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Api.Security;

public sealed record RacLoginRequest(string UserName, string Password);
public sealed record RacRiskReviewRequest(string Justification);

public static class RacAuthentication
{
    public const string Scheme = "RacSession";
    public const string CsrfHeader = "X-RAC-CSRF";

    public static IServiceCollection AddRacAuthentication(this IServiceCollection services, bool development)
    {
        services.AddSingleton<IPasswordHasher<RacUser>, PasswordHasher<RacUser>>();
        services.AddAuthentication(Scheme).AddCookie(Scheme, options =>
        {
            options.Cookie.Name = "RAC.Session";
            options.Cookie.HttpOnly = true;
            options.Cookie.Path = "/";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = development ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = false;
            options.Events.OnRedirectToLogin = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
            options.Events.OnValidatePrincipal = async ctx =>
            {
                var id = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(id, out var userId))
                {
                    ctx.RejectPrincipal();
                    return;
                }
                var factory = ctx.HttpContext.RequestServices.GetRequiredService<IDbContextFactory<RacDbContext>>();
                await using var db = await factory.CreateDbContextAsync(ctx.HttpContext.RequestAborted);
                var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ctx.HttpContext.RequestAborted);
                if (user is null || !user.Enabled ||
                    user.Role != ctx.Principal?.FindFirstValue(ClaimTypes.Role) ||
                    user.UserName != ctx.Principal?.Identity?.Name)
                    ctx.RejectPrincipal();
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy("RacOperator", p => p.RequireAuthenticatedUser()
                .RequireRole(RacRoles.Administrator, RacRoles.Operator))
            .AddPolicy("RacRiskReviewer", p => p.RequireAuthenticatedUser()
                .RequireRole(RacRoles.Administrator, RacRoles.RiskReviewer));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("RacLogin", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 6,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });
        return services;
    }

    public static IApplicationBuilder UseRacSecurity(this WebApplication app)
    {
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();

        app.Use(async (context, next) =>
        {
            if (HttpMethods.IsPost(context.Request.Method) &&
                !string.Equals(context.Request.Headers[CsrfHeader], "1", StringComparison.Ordinal))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            await next(context);
        });
        return app;
    }

    public static IEndpointRouteBuilder MapRacAuthentication(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/auth/me", (ClaimsPrincipal user) => Results.Ok(new
        {
            userName = user.Identity?.Name,
            role = user.FindFirstValue(ClaimTypes.Role),
            canControl = user.IsInRole(RacRoles.Administrator) || user.IsInRole(RacRoles.Operator),
            canReview = user.IsInRole(RacRoles.Administrator) || user.IsInRole(RacRoles.RiskReviewer)
        })).RequireAuthorization();

        endpoints.MapPost("/api/auth/login", LoginAsync).RequireRateLimiting("RacLogin");
        endpoints.MapPost("/api/auth/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(Scheme);
            return Results.Ok(new { authenticated = false });
        }).RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> LoginAsync(
        RacLoginRequest request,
        HttpContext context,
        IDbContextFactory<RacDbContext> database,
        IPasswordHasher<RacUser> passwords,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || request.UserName.Length > 80 ||
            string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024)
            return Results.Unauthorized();

        await using var db = await database.CreateDbContextAsync(ct);
        var normalized = request.UserName.Trim().ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.NormalizedUserName == normalized, ct);
        if (user is null || !user.Enabled || user.LockedUntilUtc > DateTimeOffset.UtcNow)
            return Results.Unauthorized();

        var verification = passwords.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            user.FailedLogins++;
            if (user.FailedLogins >= 5)
            {
                user.LockedUntilUtc = DateTimeOffset.UtcNow.AddMinutes(15);
                user.FailedLogins = 0;
            }
            await db.SaveChangesAsync(ct);
            return Results.Unauthorized();
        }

        user.FailedLogins = 0;
        user.LockedUntilUtc = null;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = passwords.HashPassword(user, request.Password);
        await db.SaveChangesAsync(ct);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString("D")),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role)
        };
        await context.SignInAsync(Scheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme)),
            new AuthenticationProperties
            {
                IsPersistent = false,
                AllowRefresh = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });
        return Results.Ok(new { userName = user.UserName, role = user.Role });
    }
}
