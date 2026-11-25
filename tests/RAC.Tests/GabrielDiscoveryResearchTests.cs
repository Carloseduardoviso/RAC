using RAC.Infrastructure;
using Xunit;

namespace RAC.Tests;

public sealed class GabrielDiscoveryResearchTests
{
    [Fact]
    public void MissingMatrixDoesNotInventEvidence()
    {
        var discovery=new RebecaDanielReportResult(Guid.NewGuid(),"RESEARCH_ONLY",[new(1,"ABCUSDT",2000000m,0.1m)]) {CoverageStatus="RESEARCH_CANDIDATES_AVAILABLE"};
        var row=Assert.Single(GabrielDiscoveryResearch.Assess(discovery,[]));
        Assert.Equal("ABCUSDT",row.Symbol);
        Assert.Equal("MATRIX_EVIDENCE_MISSING",row.Status);
        Assert.Equal(0,row.StressConfigurations);
        Assert.Equal(0,row.FurtherResearch);
        Assert.False(row.TradingAuthorized);
    }

    [Theory]
    [InlineData("PARTIAL_COVERAGE")]
    [InlineData("LEGACY_COVERAGE_UNKNOWN")]
    [InlineData("NO_VALID_OBSERVATIONS")]
    public void RejectsIncompleteDiscovery(string coverage)
    {
        var discovery=new RebecaDanielReportResult(Guid.NewGuid(),"RESEARCH_ONLY",[new(1,"ABCUSDT",2000000m,0.1m)]) {CoverageStatus=coverage};
        Assert.Throws<InvalidOperationException>(()=>GabrielDiscoveryResearch.Assess(discovery,[]));
    }
}
