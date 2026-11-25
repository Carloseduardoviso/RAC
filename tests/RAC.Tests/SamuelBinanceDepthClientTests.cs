using RAC.Infrastructure;
using System.Net;
namespace RAC.Tests;
public sealed class SamuelBinanceDepthClientTests
{
    private sealed class Handler(HttpStatusCode status):HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;
            Assert.Equal(HttpMethod.Get,request.Method);
            Assert.Equal("https://api.binance.com/api/v3/depth?symbol=BTCUSDT&limit=100",request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            return Task.FromResult(new HttpResponseMessage(status){
                Content=new StringContent("""{"lastUpdateId":123,"bids":[["99","2"]],"asks":[["100","3"]]}""")
            });
        }
    }
    [Fact]
    public async Task ReadsPublicDepthAndEnforcesCooldown()
    {
        var handler=new Handler(HttpStatusCode.OK);
        using var http=new HttpClient(handler);
        var client=new SamuelBinanceDepthClient(http);
        Assert.Equal(123L,(await client.ReadAsync("BTCUSDT",default)).UpdateId);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>client.ReadAsync("BTCUSDT",default));
        await Assert.ThrowsAsync<ArgumentException>(()=>client.ReadAsync("BTC&x=USDT",default));
        Assert.Equal(1,handler.Calls);
        var limited=new Handler(HttpStatusCode.TooManyRequests);
        using var limitedHttp=new HttpClient(limited);
        var other=new SamuelBinanceDepthClient(limitedHttp);
        await Assert.ThrowsAsync<HttpRequestException>(()=>other.ReadAsync("BTCUSDT",default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>other.ReadAsync("BTCUSDT",default));
        Assert.Equal(1,limited.Calls);
    }
}
