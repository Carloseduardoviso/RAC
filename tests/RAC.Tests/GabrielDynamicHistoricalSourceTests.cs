using RAC.Infrastructure;
using System.Net;
namespace RAC.Tests;
public sealed class GabrielDynamicHistoricalSourceTests
{
    private sealed class Handler(HttpStatusCode status):HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;
            Assert.Equal(HttpMethod.Get,request.Method);
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }
    [Theory]
    [InlineData(HttpStatusCode.NotFound,1)]
    [InlineData(HttpStatusCode.ServiceUnavailable,3)]
    [InlineData(HttpStatusCode.TooManyRequests,1)]
    public async Task RetriesOnlyTransientFailures(HttpStatusCode status,int calls)
    {
        using var handler=new Handler(status);
        using var http=new HttpClient(handler);
        var source=new GabrielDynamicHistoricalSource(new GabrielPublicArchiveClient(http));
        await Assert.ThrowsAsync<HttpRequestException>(()=>source.ReadAsync(new[]{"ABCUSDT"},default));
        Assert.Equal(calls,handler.Calls);
    }
}
