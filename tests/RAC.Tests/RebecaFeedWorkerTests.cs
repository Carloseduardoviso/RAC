using RAC.Api.Services;
using System.Text.Json;
namespace RAC.Tests;
public class RebecaFeedWorkerTests
{
    [Fact]
    public void FreshFeedAccepted()
    {
        var now=DateTimeOffset.UtcNow;
        var json=JsonSerializer.Serialize(new RebecaFeedWorker.Feed(now,[]));
        Assert.Empty(RebecaFeedWorker.Parse(json,now).Items);
    }
    [Fact]
    public void StaleAndFutureFeedRejected()
    {
        var now=DateTimeOffset.UtcNow;
        foreach(var time in new[]{now.AddMinutes(-6),now.AddSeconds(1)})
            Assert.Throws<InvalidDataException>(()=>RebecaFeedWorker.Parse(JsonSerializer.Serialize(new RebecaFeedWorker.Feed(time,[])),now));
    }
}
