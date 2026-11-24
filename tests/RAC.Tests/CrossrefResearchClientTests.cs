using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;
public class CrossrefResearchClientTests
{
    [Fact]
    public void DiscoversPublisherWithoutApproving()
    {
        var candidates=new ResearchSourceCandidates();
        var json="""{"message":{"items":[{"link":[{"URL":"https://publisher.example.org/paper"},{"URL":"http://unsafe.example.org"}]}]}}""";
        Assert.Equal(1,CrossrefResearchClient.Discover(json,candidates,DateTimeOffset.UtcNow));
        Assert.Equal(0,CrossrefResearchClient.Discover(json,candidates,DateTimeOffset.UtcNow));
        Assert.Single(candidates.Snapshot());
    }
}
