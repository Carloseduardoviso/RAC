using RAC.Infrastructure;
using System.Net;
namespace RAC.Tests;
public sealed class LucasBinancePublicClientTests
{
    private sealed class Handler(HttpStatusCode status,string body):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get,request.Method);
            Assert.Equal("https://api.binance.com/api/v3/time",request.RequestUri!.AbsoluteUri);
            Assert.Null(request.Headers.Authorization);
            Assert.False(request.Headers.Contains("X-MBX-APIKEY"));
            return Task.FromResult(new HttpResponseMessage(status){Content=new StringContent(body)});
        }
    }
    [Fact]
    public async Task ReadsPublicTimeAndHandlesRateLimitsAndInvalidPayload()
    {
        using var http=new HttpClient(new Handler(HttpStatusCode.OK,"{\"serverTime\":1700000000000}"));
        var result=await new LucasBinancePublicClient(http).ReadAsync(default);
        Assert.Equal(200,result.HttpStatus);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000),result.ServerUtc);
        Assert.Equal("PUBLIC",result.Environment);
        Assert.True(result.FinishedUtc>=result.StartedUtc);
        using var limited=new HttpClient(new Handler(HttpStatusCode.TooManyRequests,"{}"));
        var failure=await new LucasBinancePublicClient(limited).ReadAsync(default);
        Assert.Equal(429,failure.HttpStatus);
        Assert.Null(failure.ServerUtc);
        using var invalid=new HttpClient(new Handler(HttpStatusCode.OK,"{}"));
        Assert.Null((await new LucasBinancePublicClient(invalid).ReadAsync(default)).ServerUtc);
    }
}
