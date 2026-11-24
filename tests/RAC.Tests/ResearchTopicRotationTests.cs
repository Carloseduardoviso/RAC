using RAC.Infrastructure;
namespace RAC.Tests;
public class ResearchTopicRotationTests
{
    [Fact]
    public void SevenDaysCoverSevenTopics()
    {
        var start=new DateTimeOffset(2026,11,24,0,0,0,TimeSpan.Zero);
        Assert.Equal(7,Enumerable.Range(0,7).Select(i=>ResearchTopicRotation.ForDate(start.AddDays(i))).Distinct().Count());
        Assert.Equal(ResearchTopicRotation.ForDate(start),ResearchTopicRotation.ForDate(start.AddDays(7)));
    }
    [Fact]
    public void EndpointUsesFixedHttpsOrigin()
    {
        var uri=ResearchTopicRotation.EndpointFor(DateTimeOffset.UtcNow);
        Assert.Equal("https",uri.Scheme);
        Assert.Equal("api.crossref.org",uri.Host);
        Assert.Equal("/works",uri.AbsolutePath);
    }
}
