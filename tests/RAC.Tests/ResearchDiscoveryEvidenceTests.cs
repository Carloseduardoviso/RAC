using RAC.Infrastructure;
namespace RAC.Tests;
public class ResearchDiscoveryEvidenceTests
{
    [Fact]
    public void CrossrefReferenceDoesNotProveCredibility()
    {
        var result=ResearchDiscoveryEvidence.Inspect("example.org","https://example.org/article","crossref");
        Assert.True(result.HostMatches);
        Assert.True(result.ProviderRecognized);
        Assert.Null(result.Evidence.Provenance);
    }
    [Theory]
    [InlineData("http://example.org/article")]
    [InlineData("https://user@example.org/article")]
    [InlineData("https://other.org/article")]
    public void InvalidOrMismatchedReferenceIsNotVerified(string url)
    {
        var result=ResearchDiscoveryEvidence.Inspect("example.org",url,"crossref");
        Assert.False(result.HostMatches);
        Assert.Null(result.Evidence.Provenance);
    }
}
