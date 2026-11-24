using RAC.Infrastructure;
using System.Net;
namespace RAC.Tests;
public class FederalReserveNewsClientTests
{
    private sealed class Handler:HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal(FederalReserveNewsClient.FeedUri,request.RequestUri);
            Assert.Equal(HttpMethod.Get,request.Method);
            var date=DateTimeOffset.UtcNow.AddMinutes(-1).ToString("R");
            var xml=$"<rss><channel><item><title>Release</title><link>https://www.federalreserve.gov/news.htm</link><pubDate>{date}</pubDate></item><item><title>Other</title><link>https://example.com/news</link><pubDate>{date}</pubDate></item></channel></rss>";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){RequestMessage=request,Content=new StringContent(xml)});
        }
    }
    [Fact]
    public async Task ReadsOnlyOfficialLinks()
    {
        using var http=new HttpClient(new Handler());
        var items=await new FederalReserveNewsClient(http).ReadAsync(default);
        Assert.Single(items);Assert.Equal("fed",items[0].Source);
    }
}
