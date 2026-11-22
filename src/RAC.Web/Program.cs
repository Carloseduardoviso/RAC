using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);
var backendUrl = builder.Configuration["RacApi:BaseUrl"] ?? "http://localhost:5080";
if (!Uri.TryCreate(backendUrl, UriKind.Absolute, out var backend) ||
    (backend.Scheme != Uri.UriSchemeHttp && backend.Scheme != Uri.UriSchemeHttps))
    throw new InvalidOperationException("RacApi:BaseUrl must be a valid HTTP(S) URL.");

builder.Services.AddHttpClient("RAC.Api", client =>
{
    client.BaseAddress = backend;
    client.Timeout = TimeSpan.FromSeconds(45);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    UseCookies = false, // Never pool browser authentication cookies across users.
    AllowAutoRedirect = false
});
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

var getRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "/api/health", "/api/state", "/api/history", "/api/auth/me",
    "/api/market/overview", "/api/binance/testnet/ping",
    "/api/binance/spot/status", "/api/binance/spot/balances",
    "/api/portfolio/history", "/api/portfolio/activity/history"
};
var postRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "/api/auth/login", "/api/auth/logout", "/api/risk/review",
    "/api/control/start", "/api/control/pause", "/api/control/step", "/api/control/reset",
    "/api/portfolio/snapshot", "/api/portfolio/plan", "/api/portfolio/cashflows",
    "/api/portfolio/activity/import"
};

app.MapMethods("/api/{**path}", ["GET", "POST"], async (HttpContext context, IHttpClientFactory factory) =>
{
    var path = context.Request.Path.Value ?? "";
    var validSymbolPath = path.StartsWith("/api/binance/testnet/symbol/", StringComparison.OrdinalIgnoreCase) &&
        path["/api/binance/testnet/symbol/".Length..] is var symbol &&
        symbol.Length is > 0 and <= 12 && symbol.All(char.IsAsciiLetterOrDigit);
    var marketCandlePath = path.StartsWith("/api/market/candles/", StringComparison.OrdinalIgnoreCase) &&
        path["/api/market/candles/".Length..] is var marketSymbol &&
        marketSymbol.Length is > 0 and <= 12 && marketSymbol.All(char.IsAsciiLetterOrDigit);
    var permitted = HttpMethods.IsGet(context.Request.Method)
        ? getRoutes.Contains(path) || validSymbolPath || marketCandlePath
        : HttpMethods.IsPost(context.Request.Method) && postRoutes.Contains(path);
    if (!permitted) return Results.NotFound();

    // These routes are a fixed allowlist, not an unrestricted reverse proxy.
    using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), path);
    if (context.Request.Cookies.TryGetValue("RAC.Session", out var session))
        request.Headers.TryAddWithoutValidation("Cookie", "RAC.Session=" + session);

    if (HttpMethods.IsPost(context.Request.Method))
    {
        var maxBody = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxBody is { IsReadOnly: false }) maxBody.MaxRequestBodySize = 16_384;
        if (context.Request.ContentLength > 16_384)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
        if (!context.Request.HasJsonContentType())
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        if (context.Request.Headers["X-RAC-CSRF"] != "1")
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        request.Headers.TryAddWithoutValidation("X-RAC-CSRF", "1");
        request.Content = new StreamContent(context.Request.Body);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
    }

    using var response = await factory.CreateClient("RAC.Api")
        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
    var body = await response.Content.ReadAsByteArrayAsync(context.RequestAborted);
    if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        foreach (var cookie in setCookies) context.Response.Headers.Append("Set-Cookie", cookie);
    context.Response.Headers.CacheControl = "no-store";
    context.Response.StatusCode = (int)response.StatusCode;
    return Results.Bytes(body, response.Content.Headers.ContentType?.ToString() ?? "application/json");
});
await app.RunAsync();
