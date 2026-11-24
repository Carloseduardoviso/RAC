using RAC.Domain;
using RAC.Infrastructure;
namespace RAC.Tests;
public class ResearchProductivityTests
{
    [Fact]
    public void SeparatesFailuresFromCompletedYield()
    {
        var rows=new ResearchExecutionRecord[]{
            new(){Topic="risk",Status="COMPLETED",PublicationsFound=20,PublicationsSaved=5,ElapsedMs=100},
            new(){Topic="risk",Status="FAILED",PublicationsFound=10,PublicationsSaved=3,ElapsedMs=500}};
        var m=Assert.Single(ResearchProductivity.Summarize(rows));
        Assert.Equal(2,m.Attempts);
        Assert.Equal(1,m.Failed);
        Assert.Equal(5L,m.Saved);
        Assert.Equal(.25,m.DiscoveryRate);
        Assert.Equal(100d,m.AverageElapsedMs);
    }
    [Fact]
    public void NoCompletedResearchHasNoRate()
    {
        var m=Assert.Single(ResearchProductivity.Summarize([new(){Topic="risk",Status="FAILED"}]));
        Assert.Null(m.DiscoveryRate);
        Assert.Null(m.AverageElapsedMs);
    }
}
