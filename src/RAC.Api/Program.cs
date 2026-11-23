using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RAC.Api.Models;
using RAC.Api.Security;
using RAC.Api.Services;
using RAC.Infrastructure;
using RAC.Domain;
using RAC.Application;

var builder = WebApplication.CreateBuilder(args);
var racConnection = Environment.GetEnvironmentVariable("RAC_SQLSERVER_CONNECTION")
    ?? builder.Configuration.GetConnectionString("RAC");
if (string.IsNullOrWhiteSpace(racConnection))
    throw new InvalidOperationException("RAC requer conexão SQL Server configurada; execução bloqueada.");

builder.Services.AddDbContextFactory<RacDbContext>(options => options.UseSqlServer(racConnection));
builder.Services.AddRacAuthentication(builder.Environment.IsDevelopment());
builder.Services.AddSingleton(builder.Configuration.GetSection("Rac:Risk").Get<RiskPolicy>() ?? new RiskPolicy());
builder.Services.AddSingleton<ProductionRiskPolicyStore>();
builder.Services.AddSingleton<RacSimulator>();
builder.Services.AddSingleton<RacPersistentEngine>();
builder.Services.AddHostedService<RacWorker>();
builder.Services.AddHttpClient<BinancePublicMarketClient>(client =>
{
    client.BaseAddress = new Uri("https://data-api.binance.vision/");
    client.Timeout = TimeSpan.FromSeconds(12);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("RAC-MarketObserver/0.7");
});
builder.Services.AddSingleton<RacMarketDataService>();
builder.Services.AddSingleton<RacStrategyService>();
builder.Services.AddSingleton<RacWalkForwardService>();
builder.Services.AddSingleton<RacExtendedHistoryService>();
builder.Services.AddSingleton<RacPaperTradingService>();
builder.Services.AddHostedService<RacPaperTradingWorker>();
builder.Services.AddSingleton<RacPaperMonitoringService>();
builder.Services.AddSingleton<RacSafetyMonitoringService>();
builder.Services.AddSingleton<PaperRiskReviewService>();
builder.Services.AddHostedService<RacPaperMonitoringWorker>();
// No mainnet order endpoint can be injected into this hard-pinned Testnet client.
builder.Services.AddSingleton<BinanceTestnetCredentialStore>();
builder.Services.AddHttpClient<BinanceTestnetOrderClient>(client =>
{
    client.BaseAddress = new Uri("https://testnet.binance.vision/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("RAC-Testnet-Only/1.2");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    UseCookies = false, AllowAutoRedirect = false
});
builder.Logging.AddFilter("System.Net.Http.HttpClient.BinanceTestnetOrderClient", LogLevel.None);
builder.Services.AddSingleton<RacTestnetExecutor>();
builder.Services.AddHostedService<RacTestnetWorker>();
builder.Services.AddHostedService<RacMarketDataWorker>();
builder.Services.AddHttpClient<BinancePortfolioPricesClient>(client =>
{
    client.BaseAddress = new Uri("https://data-api.binance.vision/");
    client.Timeout = TimeSpan.FromSeconds(12);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("RAC-Spot-Portfolio-0.8");
});
builder.Services.AddSingleton<RacSpotPortfolioService>();

// Real account inspection is read-only, explicitly requested and never scheduled.
builder.Services.AddSingleton<BinanceSpotCredentialStore>();
builder.Services.AddHttpClient<BinanceSpotAccountClient>(client =>
{
    client.BaseAddress = new Uri("https://api.binance.com/");
    client.Timeout = TimeSpan.FromSeconds(12);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("RAC-Spot-Wallet-ReadOnly/0.7");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    UseCookies = false,
    AllowAutoRedirect = false
});
builder.Services.AddSingleton<RacSpotActivityService>();
builder.Services.AddHttpClient<BinanceSpotActivityClient>(client =>
{
    client.BaseAddress = new Uri("https://api.binance.com/");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("RAC-Spot-Activity-ReadOnly/0.9");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    UseCookies = false,
    AllowAutoRedirect = false
});
builder.Logging.AddFilter("System.Net.Http.HttpClient.BinanceSpotActivityClient", LogLevel.None);
// Signed URLs contain HMAC signatures: never log outgoing request URIs.
builder.Logging.AddFilter("System.Net.Http.HttpClient.BinanceSpotAccountClient", LogLevel.None);
builder.Services.AddHttpClient("binance", client =>
{
    client.BaseAddress = new Uri("https://testnet.binance.vision/");
    client.Timeout = TimeSpan.FromSeconds(8);
});
var app = builder.Build();
app.UseRacSecurity();
app.MapRacAuthentication();
app.MapProductionRiskPolicyEndpoints();

app.MapGet("/api/health", async (IDbContextFactory<RacDbContext> factory, CancellationToken ct) =>
{
    await using var db=await factory.CreateDbContextAsync(ct);
    var testnetEnabled=await db.TestnetExecutionStates.AsNoTracking()
        .AnyAsync(x=>x.Id==1 && x.Enabled && !x.CircuitLocked && !x.EmergencyStopped,ct);
    return new
    {
        status = "ok", project = "RAC", environment = "SIMULACAO_LOCAL",
        realOrdersEnabled = false, testnetOrderEnabled = testnetEnabled
    };
});

app.MapGet("/api/research/robustness", () =>
{
    try
    {
        var monthly = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "RAC", "research", "monthly");
        return Results.Ok(RacRobustnessDashboardReport.Load(monthly));
    }
    catch (Exception e) when (e is IOException or System.Text.Json.JsonException or
        ArgumentException or InvalidOperationException or KeyNotFoundException or
        FormatException or OverflowException)
    {
        return Results.Ok(new { available = false, mode = "ROBUSTNESS_INVALID",
            paperModified = false, orderPlacementEnabled = false,
            promotionAllowed = false,
            notice = "Relatório SHADOW inválido ou incompleto. Nenhuma estratégia aprovada." });
    }
}).RequireAuthorization();

app.MapGet("/api/research/frozen-decisions", () =>
{
    try
    {
        var monthly = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData), "RAC", "research", "monthly");
        return Results.Ok(RacFrozenDecisionAudit.Load(monthly));
    }
    catch (Exception e) when (e is IOException or System.Text.Json.JsonException or
        ArgumentException or InvalidOperationException or KeyNotFoundException or
        FormatException or OverflowException)
    {
        return Results.Ok(new { available = false, mode = "FROZEN_DECISION_INVALID",
            paperModified = false, orderPlacementEnabled = false,
            promotionAllowed = false, notice = "Auditoria congelada inválida. Nenhuma estratégia aprovada." });
    }
}).RequireAuthorization();

app.MapGet("/api/research/six-month-validation", () =>
{
    try
    {
        var monthly = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "RAC", "research", "monthly");
        return Results.Ok(RacShadowSixMonthValidation.Load(monthly));
    }
    catch (Exception e) when (e is IOException or System.Text.Json.JsonException or
        ArgumentException or InvalidOperationException or KeyNotFoundException or
        FormatException or OverflowException)
    {
        return Results.Ok(new {
            available = false, mode = "SIX_MONTH_VALIDATION_FAILED",
            paperModified = false, orderPlacementEnabled = false,
            promotionAllowed = false,
            notice = "Pesquisa longitudinal incompleta ou inválida: nenhuma aprovação."
        });
    }
}).RequireAuthorization();

app.MapGet("/api/research/overfit-screen", () =>
{
    try
    {
        var monthly = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "RAC", "research", "monthly");
        var history = RacShadowThreeMonthDashboard.Load(monthly);
        return Results.Ok(RacShadowOverfitDiagnostics.Assess(history));
    }
    catch (Exception e) when (e is IOException or System.Text.Json.JsonException or
        ArgumentException or InvalidOperationException or KeyNotFoundException or
        FormatException or OverflowException)
    {
        return Results.Ok(new {
            available = false, mode = "OVERFIT_SCREEN_INVALID",
            paperModified = false, orderPlacementEnabled = false,
            promotionAllowed = false,
            notice = "Evidência estatística indisponível ou inválida. Nenhuma aprovação."
        });
    }
}).RequireAuthorization();

app.MapGet("/api/research/three-month-dashboard", () =>
{
    try
    {
        var monthly = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "RAC", "research", "monthly");
        return Results.Ok(RacShadowThreeMonthDashboard.Load(monthly));
    }
    catch (Exception e) when (e is IOException or System.Text.Json.JsonException or
        ArgumentException or InvalidOperationException or KeyNotFoundException or
        FormatException or OverflowException)
    {
        return Results.Ok(new {
            available = false, mode = "REPORT_VALIDATION_FAILED",
            orderPlacementEnabled = false, promotionAllowed = false,
            notice = "Comparação histórica inválida ou incompleta. Nenhuma estratégia autorizada."
        });
    }
}).RequireAuthorization();

app.MapGet("/api/research/shadow-dashboard", () =>
{
    try
    {
        var monthly = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData),
            "RAC", "research", "monthly");
        return Results.Ok(RacShadowDashboardReport.Load(monthly, "2026-10"));
    }
    catch (Exception e) when (e is IOException or System.Text.Json.JsonException or
        ArgumentException or InvalidOperationException or KeyNotFoundException or
        FormatException or OverflowException)
    {
        return Results.Ok(new {
            available = false, mode = "REPORT_VALIDATION_FAILED",
            month = "2026-10", agents = 0,
            orderPlacementEnabled = false, promotionAllowed = false,
            notice = "Relatório SHADOW inválido ou indisponível. Nenhum resultado autorizado."
        });
    }
}).RequireAuthorization();

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

app.MapGet("/api/market/overview", async (
    RacDbContext db, RacMarketDataService feed, CancellationToken ct) =>
{
    var quotes = await db.MarketQuotes.AsNoTracking().OrderBy(x => x.Symbol)
        .Select(x => new { x.Symbol, x.PriceUsdt, x.ObservedUtc, x.Source })
        .ToListAsync(ct);
    return Results.Ok(new
    {
        feed = feed.Health, unit = "USDT",
        simulationLinked = false, orderPlacementEnabled = false,
        quotes = quotes.Select(q => new {
            q.Symbol, q.PriceUsdt, q.ObservedUtc, q.Source,
            stale = q.ObservedUtc < DateTimeOffset.UtcNow.AddMinutes(-3)
        })
    });
}).RequireAuthorization();

app.MapGet("/api/market/candles/{symbol}", async (
    string symbol, int? limit, RacDbContext db, CancellationToken ct) =>
{
    if (!RacMarketSymbols.IsSupported(symbol))
        return Results.BadRequest(new { error = "Par não monitorado pelo RAC." });
    if (limit is < 1 or > 120)
        return Results.BadRequest(new { error = "Limite permitido: 1 a 120 candles." });
    var candles = await db.MarketCandles.AsNoTracking()
        .Where(x => x.Symbol == symbol && x.Interval == RacMarketSymbols.CandleInterval)
        .OrderByDescending(x => x.OpenUtc).Take(limit ?? 40)
        .Select(x => new { x.OpenUtc, x.CloseUtc, x.Open, x.High,
            x.Low, x.Close, x.Volume, x.QuoteVolume, x.Trades })
        .ToListAsync(ct);
    candles.Reverse();
    return Results.Ok(new { symbol, interval = "1m", source = RacMarketSymbols.Source,
        onlyClosed = true, candles });
}).RequireAuthorization();

app.MapGet("/api/strategy/overview", async (
    RacStrategyService strategies, CancellationToken ct) =>
    Results.Ok(await strategies.OverviewAsync(ct)))
    .RequireAuthorization();

app.MapGet("/api/strategy/walk-forward", async (
    RacWalkForwardService service, CancellationToken ct) =>
    Results.Ok(await service.AnalyzeAsync(ct)))
    .RequireAuthorization();

app.MapGet("/api/strategy/extended-history", async (
    RacExtendedHistoryService service, CancellationToken ct) =>
    Results.Ok(await service.AnalyzeAsync(ct)))
    .RequireAuthorization();

app.MapGet("/api/strategy/shadow-comparison", async (
    RacStrategyService strategies, CancellationToken ct) =>
    Results.Ok(await strategies.ShadowComparisonAsync(ct)))
    .RequireAuthorization();

app.MapGet("/api/strategy/validation", async (
    RacStrategyService strategies, CancellationToken ct) =>
    Results.Ok(await strategies.ValidationAsync(ct)))
    .RequireAuthorization();

app.MapGet("/api/strategy/backtest/{symbol}", async (
    string symbol, RacStrategyService strategies, CancellationToken ct) =>
{
    if (!RacMarketSymbols.IsSupported(symbol))
        return Results.BadRequest(new { error = "Par não monitorado pelo RAC." });
    return Results.Ok(await strategies.BacktestAsync(symbol, ct));
}).RequireAuthorization();

app.MapGet("/api/paper/status", async (RacPaperTradingService paper, CancellationToken ct) =>
    Results.Ok(await paper.SnapshotAsync(ct))).RequireAuthorization();

app.MapPost("/api/paper/start", async (RacPaperTradingService paper,
    ClaimsPrincipal user, CancellationToken ct) =>
    await HandlePortfolioAsync(() => paper.SetRunningAsync(true,
        user.Identity?.Name ?? "operator", ct))).RequireAuthorization("RacOperator");

app.MapPost("/api/paper/pause", async (RacPaperTradingService paper,
    ClaimsPrincipal user, CancellationToken ct) =>
    await HandlePortfolioAsync(() => paper.SetRunningAsync(false,
        user.Identity?.Name ?? "operator", ct))).RequireAuthorization("RacOperator");

app.MapGet("/api/risk/safety-monitoring", async (
    RacSafetyMonitoringService monitor, HttpContext http, CancellationToken ct) =>
{
    http.Response.Headers.CacheControl = "no-store";
    try { return Results.Ok(await monitor.SnapshotAsync(ct)); }
    catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or
        Microsoft.EntityFrameworkCore.DbUpdateException)
    {
        return Results.Json(new { status = "UNAVAILABLE",
            mode = "READ_ONLY_SAFETY_DIAGNOSTICS", realOrdersEnabled = false,
            error = "SQL monitoring unavailable. No order actions permitted." },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization("RacRiskReviewer");

// Separate from /api/risk/review (legacy simulator). Evidence only:
 // no POST/review/unlock endpoint, and no PAPER state mutation.
app.MapGet("/api/paper/risk-review", async (
    PaperRiskReviewService review, HttpContext http, CancellationToken ct) =>
{
    http.Response.Headers.CacheControl = "no-store";
    try { return Results.Ok(await review.SnapshotAsync(ct)); }
    catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or
        Microsoft.EntityFrameworkCore.DbUpdateException)
    {
        return Results.Json(new { status = "UNAVAILABLE",
            mode = "READ_ONLY_PAPER_RISK_REVIEW", reviewApproved = false,
            unlockPermitted = false, realOrdersEnabled = false,
            error = "SQL review unavailable; no risk approval." },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization("RacRiskReviewer");

app.MapGet("/api/paper/monitoring", async (
    RacPaperMonitoringService monitor, CancellationToken ct) =>
    Results.Ok(await monitor.ReportAsync(ct)))
    .RequireAuthorization();

app.MapGet("/api/testnet/execution/status", async (RacTestnetExecutor executor,CancellationToken ct) =>
    Results.Ok(await executor.StatusAsync(ct))).RequireAuthorization("RacOperator");

app.MapPost("/api/testnet/execution/start", async (RacTestnetExecutor executor,CancellationToken ct) =>
    await HandlePortfolioAsync(() => executor.SetEnabledAsync(true,ct)))
    .RequireAuthorization("RacOperator");

app.MapPost("/api/testnet/execution/pause", async (RacTestnetExecutor executor,CancellationToken ct) =>
    await HandlePortfolioAsync(() => executor.SetEnabledAsync(false,ct)))
    .RequireAuthorization("RacOperator");

app.MapPost("/api/testnet/execution/emergency", async (RacTestnetExecutor executor,CancellationToken ct) =>
    await HandlePortfolioAsync(() => executor.EmergencyStopAsync(ct)))
    .RequireAuthorization("RacOperator");

app.MapGet("/api/binance/spot/status", (BinanceSpotCredentialStore store,
    HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(new { configured = store.IsConfigured,
        source = "BINANCE_SPOT_REAL", mode = "READ_ONLY",
        realOrdersEnabled = false, note = "Apenas saldos Spot; sem ordens ou transferências." });
}).RequireAuthorization("RacWalletOwner");

app.MapGet("/api/binance/spot/balances", async (BinanceSpotAccountClient wallet,
    BinanceSpotCredentialStore store, HttpContext context, CancellationToken ct) =>
{
    context.Response.Headers.CacheControl = "no-store";
    if (!store.IsConfigured)
        return Results.Json(new { error = "Configure uma chave Binance somente leitura no terminal local." },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    try { return Results.Ok(await wallet.GetWalletAsync(ct)); }
    catch (BinanceUnsafePermissionsException)
    {
        return Results.BadRequest(new { error =
            "A chave Binance possui permissões de movimentação ou não comprovou acesso somente leitura. Desative trading, saques e transferências e configure outra chave." });
    }
    catch (BinanceWalletRequestException)
    {
        return Results.Json(new { error =
            "A Binance recusou a consulta da carteira. Verifique a chave, IP permitido e permissões somente leitura." },
            statusCode: StatusCodes.Status502BadGateway);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or
        CryptographicException or InvalidOperationException or FormatException or
        System.Text.Json.JsonException)
    {
        return Results.Json(new { error =
            "Não foi possível consultar a carteira. Verifique a rede e o usuário Windows que protege as credenciais." },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}).RequireAuthorization("RacWalletOwner");

app.MapGet("/api/portfolio/history", async (
    ClaimsPrincipal user, RacSpotPortfolioService portfolio, HttpContext context, CancellationToken ct) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(await portfolio.HistoryAsync(
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), ct));
}).RequireAuthorization("RacWalletOwner");

app.MapPost("/api/portfolio/snapshot", async (
    ClaimsPrincipal user, RacSpotPortfolioService portfolio, HttpContext context, CancellationToken ct) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return await HandlePortfolioAsync(() => portfolio.CaptureAsync(
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), ct));
}).RequireAuthorization("RacWalletOwner");

app.MapPost("/api/portfolio/plan", async (
    SpotPlanRequest request, ClaimsPrincipal user,
    RacSpotPortfolioService portfolio, HttpContext context, CancellationToken ct) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return await HandlePortfolioAsync(() => portfolio.SavePlanAsync(
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), request, ct));
}).RequireAuthorization("RacWalletOwner");

app.MapPost("/api/portfolio/cashflows", async (
    SpotContributionRequest request, ClaimsPrincipal user,
    RacSpotPortfolioService portfolio, HttpContext context, CancellationToken ct) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return await HandlePortfolioAsync(() => portfolio.AddCashFlowAsync(
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), request, ct));
}).RequireAuthorization("RacWalletOwner");

app.MapGet("/api/portfolio/activity/history", async (
    ClaimsPrincipal user, RacSpotActivityService history, HttpContext context, CancellationToken ct) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return Results.Ok(await history.HistoryAsync(
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), ct));
}).RequireAuthorization("RacWalletOwner");

app.MapPost("/api/portfolio/activity/import", async (
    SpotActivityImportRequest request, ClaimsPrincipal user,
    RacSpotActivityService history, HttpContext context, CancellationToken ct) =>
{
    context.Response.Headers.CacheControl = "no-store";
    return await HandlePortfolioAsync(() => history.ImportAsync(
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), request, ct));
}).RequireAuthorization("RacWalletOwner");

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

static async Task<IResult> HandlePortfolioAsync(Func<Task<object>> operation)
{
    try { return Results.Ok(await operation()); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (InvalidOperationException ex) when (
        ex.Message.StartsWith("Aguarde 45 segundos", StringComparison.Ordinal))
    { return Results.Conflict(new { error = ex.Message }); }
    catch (BinanceUnsafePermissionsException)
    { return Results.BadRequest(new { error = "A chave Binance deve ter permissão exclusivamente de leitura." }); }
    catch (Exception)
    { return Results.Json(new { error = "Falha ao consultar ou registrar o patrimônio. Verifique a Binance e o SQL Server." }, statusCode: 503); }
}

static async Task<IResult> HandleControlAsync(Func<Task<RacState>> change)
{
    try { return Results.Ok(await change()); }
    catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
    catch (InvalidOperationException ex) { return Results.Conflict(new { error = ex.Message }); }
}
