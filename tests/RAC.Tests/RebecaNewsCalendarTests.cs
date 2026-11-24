using RAC.Application;
namespace RAC.Tests;
public class RebecaNewsCalendarTests
{
    [Fact]
    public void MissingAndExpiredAreUnavailable()
    {
        var c=new RebecaNewsCalendar();var now=DateTimeOffset.UtcNow;
        Assert.Null(c.EventsAround("BTCUSDT",now));
        c.Publish(now,[]);
        Assert.Empty(c.EventsAround("BTCUSDT",now)!);
        Assert.Null(c.EventsAround("BTCUSDT",now.AddMinutes(6)));
        Assert.Null(c.EventsAround("BTCUSDT",now.AddSeconds(-1)));
    }
    [Fact]
    public void FiltersSymbolAndGlobalEvents()
    {
        var c=new RebecaNewsCalendar();var now=DateTimeOffset.UtcNow;
        c.Publish(now,[new("BTCUSDT",new("BTC event",now,"high")),new("ALL",new("Global",now,"medium"))]);
        Assert.Equal(2,c.EventsAround("BTCUSDT",now)!.Count);
        Assert.Single(c.EventsAround("ETHUSDT",now)!);
        Assert.Throws<InvalidOperationException>(()=>c.Publish(now.AddSeconds(-1),[]));
    }
}
