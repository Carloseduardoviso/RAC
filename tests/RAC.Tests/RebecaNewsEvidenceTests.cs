using RAC.Application;
using Xunit;
namespace RAC.Tests;
public sealed class RebecaNewsEvidenceTests
{
    [Fact]
    public void DistinguishesUnavailableCalendarAndDeduplicatesEvents()
    {
        var now=DateTimeOffset.UtcNow;
        var calendar=new RebecaNewsCalendar();
        Assert.Equal("NEWS_UNAVAILABLE",RebecaNewsEvidence.Review(calendar,"BTCUSDT",now).Status);
        var e=new RacNewsEvent("Event",now.AddMinutes(2),"high");
        calendar.Publish(now,[new("ALL",e),new("BTCUSDT",e),new("ETHUSDT",new("Other",now,"high"))]);
        var result=RebecaNewsEvidence.Review(calendar,"BTCUSDT",now);
        Assert.Equal("HIGH_IMPACT_REVIEW",result.Status);
        Assert.Equal(e,Assert.Single(result.Events));
        Assert.False(result.TradingAuthorized);
        Assert.Equal("NEWS_UNAVAILABLE",RebecaNewsEvidence.Review(calendar,"BTCUSDT",now.AddMinutes(6)).Status);
        calendar.Publish(now.AddSeconds(1),[new("ALL",e with {Impact="low"}),new("ALL",e with {Impact="medium"})]);
        Assert.Equal("NEWS_CONFLICT",RebecaNewsEvidence.Review(calendar,"BTCUSDT",now.AddSeconds(1)).Status);
        calendar.Publish(now.AddSeconds(2),[]);
        Assert.Equal("NO_NEARBY_EVENTS",RebecaNewsEvidence.Review(calendar,"BTCUSDT",now.AddSeconds(2)).Status);
    }
}
