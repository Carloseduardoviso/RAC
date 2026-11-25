using RAC.Application;
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
    [Fact]
    public void MatchesOnlyDiscoveredSymbolsAndStressConfigurations()
    {
        var discovery=new RebecaDanielReportResult(Guid.NewGuid(),"RESEARCH_ONLY",[new(1,"BTCUSDT",3000000m,0.1m),new(2,"ETHUSDT",2000000m,0.1m),new(3,"ABCUSDT",1000000m,0.1m)]) {CoverageStatus="RESEARCH_CANDIDATES_AVAILABLE"};
        var p=ResearchExperimentPlan.Build().First().Parameters;
        GabrielExperimentMatrix.Assessment Row(string symbol,decimal mean,int unresolved,decimal slip)=>new(symbol,p with {SlippagePctPerSide=slip},new(6,mean,1,2,40,20,unresolved,"EXPLORATORY_ONLY"));
        var result=GabrielDiscoveryResearch.Assess(discovery,[Row("BTCUSDT",1,0,.20m),Row("BTCUSDT",9,1,.20m),Row("BTCUSDT",20,0,.05m),Row("ETHUSDT",-1,0,.20m),Row("SOLUSDT",2,0,.20m)]);
        Assert.Equal(new[]{"BTCUSDT","ETHUSDT","ABCUSDT"},result.Select(x=>x.Symbol));
        Assert.Equal("INDEPENDENT_VALIDATION_REQUIRED",result[0].Status);
        Assert.Equal(2,result[0].StressConfigurations);
        Assert.Equal(1,result[0].FurtherResearch);
        Assert.Equal("NO_QUALIFYING_CONFIGURATION",result[1].Status);
        Assert.Equal(0,result[1].FurtherResearch);
        Assert.Equal("MATRIX_EVIDENCE_MISSING",result[2].Status);
        Assert.All(result,x=>Assert.False(x.TradingAuthorized));
    }
}
