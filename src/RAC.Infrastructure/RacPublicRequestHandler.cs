using System.Net;
namespace RAC.Infrastructure;
public sealed class RacPublicRequestHandler(RacPublicRequestGate gate,TimeProvider clock):DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        var uri=request.RequestUri;
        if(uri is null || uri.Scheme!="https" || !uri.IsDefaultPort || uri.UserInfo.Length!=0 ||
            request.Method!=HttpMethod.Get || request.Headers.Authorization is not null ||
            request.Headers.Contains("X-MBX-APIKEY") || request.Content is not null ||
            !(uri.Host=="api.binance.com" && uri.AbsolutePath is ("/api/v3/time" or "/api/v3/exchangeInfo" or "/api/v3/ticker/24hr" or "/api/v3/depth")
            || uri.Host=="testnet.binance.vision" && uri.AbsolutePath=="/api/v3/time"))
            throw new InvalidOperationException("Only anonymous public market requests allowed.");
        if(!gate.TryAcquire(uri.Host,TimeSpan.FromSeconds(2),out var remaining))
        {
            // Brief spacing is allowed; longer cooldowns fail closed without contacting the exchange.
            if(remaining>TimeSpan.FromSeconds(2)) throw new HttpRequestException("Shared exchange cooldown active.");
            await Task.Delay(remaining,clock,ct);
            if(!gate.TryAcquire(uri.Host,TimeSpan.FromSeconds(2),out _))
                throw new HttpRequestException("Shared exchange cooldown active.");
        }
        try
        {
            var response=await base.SendAsync(request,ct);
            if((int)response.StatusCode is 429 or 418)
            {
                var retry=response.Headers.RetryAfter;
                var delay=retry?.Delta ?? (retry?.Date is DateTimeOffset date?date-clock.GetUtcNow():TimeSpan.FromMinutes(5));
                gate.Defer(uri.Host,delay>TimeSpan.FromSeconds(2)?delay:TimeSpan.FromSeconds(2));
            }
            else if(!response.IsSuccessStatusCode) gate.Defer(uri.Host,TimeSpan.FromSeconds(30));
            return response;
        }
        catch(HttpRequestException){gate.Defer(uri.Host,TimeSpan.FromSeconds(30));throw;}
        catch(OperationCanceledException){gate.Defer(uri.Host,TimeSpan.FromSeconds(30));throw;}
    }
    public static HttpClient CreateClient(RacPublicRequestGate gate,int buffer)=>new(new RacPublicRequestHandler(gate,TimeProvider.System)
    {InnerHandler=new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}}){MaxResponseContentBufferSize=buffer};
}
