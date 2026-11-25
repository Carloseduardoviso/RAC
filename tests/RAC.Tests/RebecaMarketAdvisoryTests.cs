using RAC.Application;
using RAC.Infrastructure;
using Xunit;
namespace RAC.Tests;
public sealed class RebecaMarketAdvisoryTests
{
    [Fact]
    public void CombinesEvidenceWithoutAuthorizingTrading()
    {
        var now=DateTimeOffset.UtcNow;
        var calendar=new RebecaNewsCalendar();
        var d=new RebecaDanielReportResult(Guid.NewGuid(),"RESEARCH_ONLY",[new(1,"BTCUSDT",2000000m,0.1m)]);
        var g=new RebecaGabrielReportResult(Guid.NewGuid(),"INDEPENDENT_VALIDATION_REQUIRED",20,10,new string('A',64));
        var missing=RebecaMarketAdvisory.Summarize([d],[],calendar,now);
        Assert.Equal("INCOMPLETE_EVIDENCE",missing.Status);
        Assert.Contains("GABRIEL:MISSING",missing.Reasons);
        Assert.Equal("INCOMPLETE_EVIDENCE",RebecaMarketAdvisory.Summarize([d],[g],calendar,now).Status);
        calendar.Publish(now,[new("ALL",new("Event",now,"high"))]);
        var critical=RebecaMarketAdvisory.Summarize([d],[g],calendar,now);
        Assert.Equal("CRITICAL_REVIEW",critical.Status);
        Assert.Contains("NEWS:BTCUSDT:HIGH_IMPACT_REVIEW",critical.Reasons);
        Assert.Equal(2,critical.EvidenceIds.Count);
        Assert.False(critical.TradingAuthorized);
        calendar.Publish(now.AddSeconds(1),[]);
        Assert.Equal("INCOMPLETE_EVIDENCE",RebecaMarketAdvisory.Summarize([d],[g],calendar,now.AddSeconds(1)).Status);
        d=d with {CoverageStatus="RESEARCH_CANDIDATES_AVAILABLE"};
        var review=RebecaMarketAdvisory.Summarize([d],[g],calendar,now.AddSeconds(1));
        Assert.Equal("HUMAN_REVIEW_REQUIRED",review.Status);
        Assert.False(review.TradingAuthorized);
        Assert.Equal("CRITICAL_REVIEW",RebecaMarketAdvisory.Summarize([d],[g with {Status="INVALID_REPORT"}],calendar,now.AddSeconds(1)).Status);
        Assert.Equal("INCOMPLETE_EVIDENCE",RebecaMarketAdvisory.Summarize([d,d],[g],calendar,now.AddSeconds(1)).Status);
    }
    [Fact]
    public void CapturedCalendarDoesNotMixUpdatesAcrossCandidates()
    {
        var now=DateTimeOffset.UtcNow;
        var calendar=new RebecaNewsCalendar();
        calendar.Publish(now,[new("ALL",new("Original",now,"high"))]);
        var captured=calendar.Capture();
        calendar.Publish(now.AddSeconds(1),[]);
        Assert.Equal("HIGH_IMPACT_REVIEW",RebecaNewsEvidence.Review(captured,"BTCUSDT",now.AddSeconds(1)).Status);
        Assert.Equal("HIGH_IMPACT_REVIEW",RebecaNewsEvidence.Review(captured,"ETHUSDT",now.AddSeconds(1)).Status);
        Assert.Equal("NO_NEARBY_EVENTS",RebecaNewsEvidence.Review(calendar,"ETHUSDT",now.AddSeconds(1)).Status);
        Assert.Equal("NEWS_UNAVAILABLE",RebecaNewsEvidence.Review(captured,"BTCUSDT",now.AddMinutes(6)).Status);
    }
}
