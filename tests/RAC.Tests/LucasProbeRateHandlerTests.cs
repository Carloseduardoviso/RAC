using RAC.Infrastructure;
using System.Net;
using System.Net.Http.Headers;
namespace RAC.Tests;
public sealed class LucasProbeRateHandlerTests
{
    private sealed class Clock:TimeProvider
    {
        public DateTimeOffset Now=DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow()=>Now;
    }
    private sealed class Transport:HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;
            var response=new HttpResponseMessage(Calls==1?HttpStatusCode.TooManyRequests:HttpStatusCode.OK);
            if(Calls==1) response.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromMinutes(3));
            return Task.FromResult(response);
        }
    }
    [Fact]
    public async Task HonorsCooldownAcrossEnvironmentsAndRejectsOtherEndpoints()
    {
        var clock=new Clock();
        var transport=new Transport();
        using var http=new HttpClient(new LucasProbeRateHandler(clock){InnerHandler=transport});
        const string spot="https://api.binance.com/api/v3/time";
        const string testnet="https://testnet.binance.vision/api/v3/time";
        Assert.Equal(HttpStatusCode.TooManyRequests,(await http.GetAsync(spot)).StatusCode);
        clock.Now=clock.Now.AddMinutes(2);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await http.GetAsync(testnet)).StatusCode);
        Assert.Equal(1,transport.Calls);
        clock.Now=clock.Now.AddMinutes(1);
        Assert.Equal(HttpStatusCode.OK,(await http.GetAsync(testnet)).StatusCode);
        Assert.Equal(2,transport.Calls);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>http.GetAsync("https://api.binance.com/api/v3/account"));
        Assert.Equal(2,transport.Calls);
    }
}
