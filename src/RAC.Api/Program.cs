using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RAC.Api.Models;
using RAC.Api.Security;
using RAC.Api.Services;
using RAC.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var racConnection = Environment.GetEnvironmentVariable("RAC_SQLSERVER_CONNECTION")
    ?? builder.Configuration.GetConnectionString("RAC");
if (string.IsNullOrWhiteSpace(racConnection))
    throw new InvalidOperationException("RAC requer conexão SQL Server configurada; execução bloqueada.");

builder.Services.AddDbContextFactory<RacDbContext>(options => options.UseSqlServer(racConnection));
builder.Services.AddRacAuthentication(builder.Environment.IsDevelopment());
builder.Services.AddSingleton(builder.Configuration.GetSection("Rac:Risk").Get<RiskPolicy>() ?? new RiskPolicy());
builder.Services.AddSingleton<RacSimulator>();
builder.Services.AddSingleton<RacPersistentEngine>();
builder.Services.AddHostedService<RacWorker>();
builder.Services.AddHttpClient("binance", client =>
{
    client.BaseAddress = new Uri("https://testnet.binance.vision/");
    client.Timeout = TimeSpan.FromSeconds(8);
});
var app = builder.Build();
app.UseRacSecurity();
app.MapRacAuthentication();

app.MapGet("/api/health", () => new
{
    status = "ok", project = "RAC", environment = "SIMULACAO_LOCAL",
    realOrdersEnabled = false, testnetOrderEnabled = false
});

app.MapGet("/api/state", (RacPersistentEngine engine, CancellationToken ct) =>
    engine.SnapshotAsync(ct)).RequireAuthorization();

app.MapPost("/api/control/start", (RacPersistentEngine engine, ClaimsPrincipal user, CancellationToken ct) =>
    HandleControlAsync(() => engine.StartAsync(ct, user.Identity?.Name))).RequireAuthorization("RacOperator");
app.MapPost("/api/control/pause", (RacPersistentEngine engine, ClaimsPrincipal user, CancellationToken ct) =>
    HandleControlAsync(() => engine.PauseAsync(ct, user.Identity?.Name))).RequireAuthorization("RacOperator");
app.MapPost("/api/control/step", (RacPersistentEngine engine, ClaimsPrincipal user, CancellationToken ct) =>
    HandleControlAsync(() => engine.StepAsync(ct, user.Identity?.Name))).RequireAuthorization("RacOperator");
app.MapPost("/api/control/reset", (RacPersistentEngine engine, ClaimsPrincipal user, CancellationToken ct) =>
    HandleControlAsync(() => engine.ResetAsync(ct, user.Identity?.Name))).RequireAuthorization("RacOperator");

app.MapPost("/api/risk/review", (RacRiskReviewRequest request,
    RacPersistentEngine engine, ClaimsPrincipal user, CancellationToken ct) =>
    HandleControlAsync(() => engine.ReviewAndResetAsync(
        user.Identity?.Name ?? "reviewer", request?.Justification ?? string.Empty, ct)))
    .RequireAuthorization("RacRiskReviewer");

app.MapGet("/api/history", async (RacDbContext db, CancellationToken ct) => new
{
    equity = await db.EquitySnapshots.AsNoTracking().OrderByDescending(x => x.Id).Take(100)
        .Select(x => new { x.EquityBrl, x.TotalContributionsBrl, x.DrawdownPercent, x.RecordedUtc }).ToListAsync(ct),
    orders = await db.Orders.AsNoTracking().OrderByDescending(x => x.CreatedUtc).Take(30)
        .Select(x => new { x.ClientOrderId, x.Symbol, x.Side, x.Quantity, x.Price, x.CreatedUtc, x.Mode }).ToListAsync(ct),
    contributions = await db.Contributions.AsNoTracking().OrderByDescending(x => x.OccurredUtc).Take(30)
        .Select(x => new { x.AmountBrl, x.IsVirtual, x.OccurredUtc }).ToListAsync(ct),
    riskLocks = await db.RiskLocks.AsNoTracking().OrderByDescending(x => x.CreatedUtc).Take(20)
        .Select(x => new { x.Reason, x.Active, x.CreatedUtc, x.ReleasedUtc }).ToListAsync(ct)
}).RequireAuthorization();

app.MapGet("/api/binance/testnet/ping", async (IHttpClientFactory factory, CancellationToken ct) =>
{
    try
    {
        using var response = await factory.CreateClient("binance").GetAsync("api/v3/ping", ct);
        return Results.Ok(new { connected = response.IsSuccessStatusCode, httpStatus = (int)response.StatusCode,
            mode = "TESTNET_PUBLIC_READ_ONLY", orderPlacementEnabled = false });
    }
    catch (HttpRequestException) { return Results.Json(new { connected = false, reason = "Falha de rede ou DNS" }, statusCode: 503); }
    catch (TaskCanceledException) { return Results.Json(new { connected = false, reason = "Tempo limite" }, statusCode: 503); }
}).RequireAuthorization();

app.MapGet("/api/binance/testnet/symbol/{symbol}", async (string symbol, IHttpClientFactory factory, CancellationToken ct) =>
{
    var allowed = new[] { "BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT", "XRPUSDT", "ADAUSDT",
        "DOGEUSDT", "LINKUSDT", "AVAXUSDT", "LTCUSDT", "DOTUSDT", "TRXUSDT" };
    if (!allowed.Contains(symbol, StringComparer.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "Par nao autorizado" });
    try
    {
        using var response = await factory.CreateClient("binance")
            .GetAsync($"api/v3/exchangeInfo?symbol={symbol.ToUpperInvariant()}", ct);
        if (!response.IsSuccessStatusCode)
            return Results.Json(new { error = "Falha na consulta de filtros", httpStatus = (int)response.StatusCode }, statusCode: 502);
        var json = await response.Content.ReadAsStringAsync(ct);
        return Results.Content(json, "application/json");
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        return Results.Json(new { error = "Nao foi possivel consultar a Testnet" }, statusCode: 503);
    }
}).RequireAuthorization();

// No routes for real orders, credentials or Testnet order placement.
await app.RunAsync();

static async Task<IResult> HandleControlAsync(Func<Task<RacState>> change)
{
    try { return Results.Ok(await change()); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}
