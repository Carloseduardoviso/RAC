using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);
var backendUrl = builder.Configuration["RacApi:BaseUrl"] ?? "http://localhost:5080";
if (!Uri.TryCreate(backendUrl, UriKind.Absolute, out var backend) ||
    (backend.Scheme != Uri.UriSchemeHttp && backend.Scheme != Uri.UriSchemeHttps))
    throw new InvalidOperationException("RacApi:BaseUrl must be a valid HTTP(S) URL.");

builder.Services.AddHttpClient("RAC.Api", client =>
{
    client.BaseAddress = backend;
    client.Timeout = TimeSpan.FromSeconds(15);
});
var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

// Requests from the browser stay on the same origin; only allow explicitly defined API paths.
var getRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "/api/health", "/api/state", "/api/history", "/api/binance/testnet/ping"
};
var postRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "/api/control/start", "/api/control/pause", "/api/control/step", "/api/control/reset"
};
app.MapMethods("/api/{**path}", ["GET", "POST"], async (HttpContext context, IHttpClientFactory factory) =>
{
    var path = context.Request.Path.Value ?? "";
    var validSymbolPath = path.StartsWith("/api/binance/testnet/symbol/", StringComparison.OrdinalIgnoreCase) &&
        path["/api/binance/testnet/symbol/".Length..] is var symbol &&
        symbol.Length is > 0 and <= 12 && symbol.All(char.IsAsciiLetterOrDigit);

    var permitted = context.Request.Method == "GET" ? getRoutes.Contains(path) || validSymbolPath :
        context.Request.Method == "POST" && postRoutes.Contains(path);
    if (!permitted) return Results.NotFound();

    using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), path);
    using var response = await factory.CreateClient("RAC.Api")
        .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
    var body = await response.Content.ReadAsByteArrayAsync(context.RequestAborted);
    var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
    context.Response.StatusCode = (int)response.StatusCode;
    return Results.Bytes(body, contentType);
});
await app.RunAsync();

