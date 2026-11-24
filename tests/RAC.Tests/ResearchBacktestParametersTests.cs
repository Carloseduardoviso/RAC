using RAC.Infrastructure;
namespace RAC.Tests;
public class ResearchBacktestParametersTests
{
    [Theory]
    [InlineData("MA_CROSS_V1")]
    [InlineData("MEAN_REVERSION_V1")]
    [InlineData("MOMENTUM_V1")]
    public void GeneratesBoundedResearchOnlyGrid(string id)
    {
        var candidate=new ResearchStrategyCandidates.Candidate("10.1234/a","test",id,"entry","exit","HYPOTHESIS_UNVALIDATED",false);
        var rows=ResearchBacktestParameters.Generate(candidate);
        Assert.Equal(8,rows.Count);
        Assert.Equal(8,rows.Distinct().Count());
        Assert.All(rows,x=>{Assert.False(x.TradingAuthorized);Assert.Equal("RESEARCH_ONLY",x.Status);Assert.Equal(0.10m,x.FeePctPerSide);Assert.Equal(10m,x.ExposurePct);Assert.True(x.StopPct>0);});
    }
    [Fact]
    public void RejectsUnknownOrAuthorizedCandidate()
    {
        var c=new ResearchStrategyCandidates.Candidate("10.1234/a","test","UNKNOWN","entry","exit","HYPOTHESIS_UNVALIDATED",false);
        Assert.Empty(ResearchBacktestParameters.Generate(c));
        Assert.Empty(ResearchBacktestParameters.Generate(c with {TemplateId="MA_CROSS_V1",TradingAuthorized=true}));
    }
}
