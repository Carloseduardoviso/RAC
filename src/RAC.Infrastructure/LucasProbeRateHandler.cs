using System.Net;
namespace RAC.Infrastructure;
public sealed class LucasProbeRateHandler(TimeProvider clock):DelegatingHandler
{
    private readonly SemaphoreSlim gate=new(1,1);
    private DateTimeOffset nextAllowed=DateTimeOffset.MinValue;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        var uri=request.RequestUri;
        if(request.Method!=HttpMethod.Get || uri is null ||
            (uri.AbsoluteUri!="https://api.binance.com/api/v3/time" &&
             uri.AbsoluteUri!="https://testnet.binance.vision/api/v3/time") ||
            request.Headers.Authorization is not null || request.Headers.Contains("X-MBX-APIKEY"))
            throw new InvalidOperationException("Only anonymous exchange time probes are allowed.");
        await gate.WaitAsync(ct);
        try
        {
            var now=clock.GetUtcNow();
            if(now<nextAllowed)
                return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            nextAllowed=now.AddSeconds(2);
            try
            {
                var response=await base.SendAsync(request,ct);
                if((int)response.StatusCode is 429 or 418)
                {
                    var retry=response.Headers.RetryAfter;
                    var until=retry?.Date ?? clock.GetUtcNow()+(retry?.Delta ?? TimeSpan.FromMinutes(5));
                    var minimum=clock.GetUtcNow().AddSeconds(2);
                    nextAllowed=until>minimum?until:minimum;
                }
                else if(!response.IsSuccessStatusCode)
                    nextAllowed=clock.GetUtcNow().AddSeconds(30);
                return response;
            }
            catch(HttpRequestException)
            {
                nextAllowed=clock.GetUtcNow().AddSeconds(30);
                throw;
            }
        }
        finally {gate.Release();}
    }
    public static HttpClient CreateClient()=>new(new LucasProbeRateHandler(TimeProvider.System)
    {
        InnerHandler=new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}
    }){MaxResponseContentBufferSize=4096};
}
