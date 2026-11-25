using System.Net;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class RacPublicRequestConcurrencyTests
{
    private sealed class Stub : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            var response = new HttpResponseMessage((HttpStatusCode)418);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(3));
            return Task.FromResult(response);
        }
    }
    [Fact]
    public async Task ThreePublicClientsShareBanAndFailClosed()
    {
        var gate = new RacPublicRequestGate(TimeProvider.System);
        var budget = new RacPublicWeightBudget(TimeProvider.System);
        var stub = new Stub();
        using var daniel = new HttpClient(new RacPublicRequestHandler(gate, TimeProvider.System, budget){InnerHandler=stub});
        using var samuel = new HttpClient(new RacPublicRequestHandler(gate, TimeProvider.System, budget){InnerHandler=stub});
        using var lucas = new HttpClient(new RacPublicRequestHandler(gate, TimeProvider.System, budget){InnerHandler=stub});
        async Task<bool> Query(HttpClient client, string path)
        {
            try { using var response = await client.GetAsync("https://api.binance.com/api/v3/" + path); return (int)response.StatusCode == 418; }
            catch(HttpRequestException) { return false; }
        }
        var results = await Task.WhenAll(Task.Run(()=>Query(daniel,"ticker/24hr")), Task.Run(()=>Query(samuel,"depth?symbol=BTCUSDT")), Task.Run(()=>Query(lucas,"time")));
        Assert.Single(results, x=>x);
        Assert.Equal(1,stub.Calls);
    }
}
