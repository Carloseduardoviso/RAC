using RAC.Application;
namespace RAC.Tests;
public sealed class ResearchEngineCatalogTests
{
    [Fact]
    public void CurrentVersionPreservesHistoricalIdentityAndResearchOnlyMode()
    {
        var version=ResearchEngineCatalog.Resolve("REPLAY_V1");
        Assert.Equal(ResearchExperimentReplay.HistoricalEngineVersion,version.Id);
        Assert.Equal("RESEARCH_ONLY",version.Mode);
        Assert.False(version.QualityFilters);
        Assert.False(version.TradingAuthorized);
    }
    [Theory]
    [InlineData("REPLAY_V2")]
    [InlineData("replay_v1")]
    [InlineData("")]
    public void UnknownVersionsAreRejected(string id)
        =>Assert.Throws<ArgumentException>(()=>ResearchEngineCatalog.Resolve(id));
}
