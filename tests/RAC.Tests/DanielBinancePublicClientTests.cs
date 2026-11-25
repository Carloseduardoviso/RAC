using RAC.Infrastructure;
using System.Net;
namespace RAC.Tests;

public sealed class DanielBinancePublicClientTests
{
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get,request.Method);
            Assert.Equal("api.binance.com",request.RequestUri!.Host);
            Assert.Null(request.Headers.Authorization);
            Assert.False(request.Headers.Contains("X-MBX-APIKEY"));
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        }
    }
    [Fact]
    public async Task RateLimitStopsFurtherRequests()
    {
        using var handler=new Handler();
        using var http=new HttpClient(handler);
        var client=new DanielBinancePublicClient(http);
        await Assert.ThrowsAsync<HttpRequestException>(()=>client.ReadAsync(default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>client.ReadAsync(default));
        Assert.Equal(1,handler.Calls);
    }
}
