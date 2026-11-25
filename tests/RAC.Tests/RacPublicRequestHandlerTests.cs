using System.Net;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class RacPublicRequestHandlerTests
{
    private sealed class Stub(HttpStatusCode status):HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;
            var response=new HttpResponseMessage(status);
            response.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(3));
            return Task.FromResult(response);
        }
    }
    [Fact]
    public async Task SharedBlockPreventsOtherClientRequestsButNotTestnet()
    {
        var gate=new RacPublicRequestGate(TimeProvider.System);
        var limited=new Stub(HttpStatusCode.TooManyRequests);
        var other=new Stub(HttpStatusCode.OK);
        using var first=new HttpClient(new RacPublicRequestHandler(gate,TimeProvider.System){InnerHandler=limited});
        using var second=new HttpClient(new RacPublicRequestHandler(gate,TimeProvider.System){InnerHandler=other});
        Assert.Equal(HttpStatusCode.TooManyRequests,(await first.GetAsync("https://api.binance.com/api/v3/time")).StatusCode);
        await Assert.ThrowsAsync<HttpRequestException>(()=>second.GetAsync("https://api.binance.com/api/v3/depth?symbol=BTCUSDT"));
        Assert.Equal(0,other.Calls);
        Assert.Equal(HttpStatusCode.OK,(await second.GetAsync("https://testnet.binance.vision/api/v3/time")).StatusCode);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>second.GetAsync("https://api.binance.com/api/v3/account"));
        Assert.Equal(1,other.Calls);
    }
}
