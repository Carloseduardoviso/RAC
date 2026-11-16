using RAC.Api.Models;
using RAC.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(builder.Configuration.GetSection("Rac:Risk").Get<RiskPolicy>() ?? new RiskPolicy());
builder.Services.AddSingleton<RacSimulator>();
builder.Services.AddHostedService<RacWorker>();
builder.Services.AddHttpClient("binance", client =>
{
    client.BaseAddress = new Uri("https://testnet.binance.vision/");
    client.Timeout = TimeSpan.FromSeconds(8);
});
var app = builder.Build();
// RAC.Web serves the frontend separately.
app.MapGet("/api/health", () => new { status = "ok", project = "RAC", environment = "SIMULACAO_LOCAL", realOrdersEnabled = false, testnetOrderEnabled = false });
app.MapGet("/api/state", (RacSimulator s) => s.Snapshot());
app.MapPost("/api/control/start", (RacSimulator s) => { s.Start(); return s.Snapshot(); });
app.MapPost("/api/control/pause", (RacSimulator s) => { s.Pause(); return s.Snapshot(); });
app.MapPost("/api/control/step", (RacSimulator s) => { s.Tick(); return s.Snapshot(); });
app.MapPost("/api/control/reset", (RacSimulator s) => { s.Reset(); return s.Snapshot(); });
app.MapGet("/api/binance/testnet/ping", async (IHttpClientFactory factory, CancellationToken ct) =>
{
    try
    {
        var response = await factory.CreateClient("binance").GetAsync("api/v3/ping", ct);
        return Results.Ok(new { connected = response.IsSuccessStatusCode, httpStatus = (int)response.StatusCode, mode = "TESTNET_PUBLIC_READ_ONLY", orderPlacementEnabled = false });
    }
    catch (HttpRequestException) { return Results.Json(new { connected = false, reason = "Falha de rede ou DNS" }, statusCode: 503); }
    catch (TaskCanceledException) { return Results.Json(new { connected = false, reason = "Tempo limite" }, statusCode: 503); }
});
app.MapGet("/api/binance/testnet/symbol/{symbol}", async (string symbol, IHttpClientFactory factory, CancellationToken ct) =>
{
    var allowed = new[] { "BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT", "XRPUSDT", "ADAUSDT", "DOGEUSDT", "LINKUSDT", "AVAXUSDT", "LTCUSDT", "DOTUSDT", "TRXUSDT" };
    if (!allowed.Contains(symbol, StringComparer.OrdinalIgnoreCase)) return Results.BadRequest(new { error = "Par nao autorizado" });
    try
    {
        var response = await factory.CreateClient("binance").GetAsync($"api/v3/exchangeInfo?symbol={symbol.ToUpperInvariant()}", ct);
        if (!response.IsSuccessStatusCode) return Results.Json(new { error = "Falha na consulta de filtros", httpStatus = (int)response.StatusCode }, statusCode: 502);
        var json = await response.Content.ReadAsStringAsync(ct);
        return Results.Content(json, "application/json");
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException) { return Results.Json(new { error = "Nao foi possivel consultar a Testnet" }, statusCode: 503); }
});
// Intencionalmente NAO existem endpoints de envio de ordens, credenciais ou modo REAL.
await app.RunAsync();

