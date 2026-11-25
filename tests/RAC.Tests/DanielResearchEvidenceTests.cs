using RAC.Infrastructure;

namespace RAC.Tests;

public sealed class DanielResearchEvidenceTests
{
    [Theory]
    [InlineData(0,2,0,0,"NO_VALID_OBSERVATIONS")]
    [InlineData(1,1,0,0,"PARTIAL_COVERAGE")]
    [InlineData(2,0,0,0,"NO_RESEARCH_CANDIDATES")]
    [InlineData(2,0,0,1,"RESEARCH_CANDIDATES_AVAILABLE")]
    [InlineData(1,0,1,1,"PARTIAL_COVERAGE")]
    public void DistinguishesCoverageFromCandidateAvailability(int accepted,int missing,int rejected,int candidates,string expected)
    {
        var json=System.Text.Json.JsonSerializer.Serialize(new {Schema="DANIEL_COVERAGE_V1",ExpectedMarkets=2,Snapshot=new {Decisions=new int[accepted]},MissingTickers=new int[missing],RejectedTickers=new int[rejected]});
        Assert.Equal(expected,DanielResearchEvidence.Classify(json,candidates));
    }

    [Fact]
    public void PreservesLegacyUnknownAndRejectsInconsistentCounts()
    {
        Assert.Equal("LEGACY_COVERAGE_UNKNOWN",DanielResearchEvidence.Classify(null,0));
        Assert.Equal("LEGACY_COVERAGE_UNKNOWN",DanielResearchEvidence.Classify("{\"Decisions\":[]}",0));
        Assert.Throws<FormatException>(()=>DanielResearchEvidence.Classify("{\"Schema\":\"DANIEL_COVERAGE_V1\",\"ExpectedMarkets\":2,\"Snapshot\":{\"Decisions\":[]},\"MissingTickers\":[],\"RejectedTickers\":[]}",0));
    }
}
