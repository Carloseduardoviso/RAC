using RAC.Application;
namespace RAC.Tests;
public class ResearchSourceReliabilityTests
{
    [Fact]
    public void UnknownEvidenceCannotPass()
    {
        var result=ResearchSourceReliability.Evaluate(new(null,true,true,true,true,true));
        Assert.Equal("INSUFFICIENT_EVIDENCE",result.Status);
        Assert.False(result.TradingAuthorized);
    }
    [Fact]
    public void FullEvidenceOnlyQualifiesForReview()
    {
        var result=ResearchSourceReliability.Evaluate(new(true,true,true,true,true,true));
        Assert.Equal(100,result.Score);
        Assert.Equal("REVIEW_CANDIDATE",result.Status);
        Assert.False(result.TradingAuthorized);
    }
    [Fact]
    public void FalseProvenanceRejects()
    {
        Assert.Equal("REJECTED",ResearchSourceReliability.Evaluate(new(false,true,true,true,true,true)).Status);
    }
    [Fact]
    public void UndisclosedConflictPreventsQualification()
    {
        Assert.Equal("INSUFFICIENT_EVIDENCE",ResearchSourceReliability.Evaluate(new(true,true,true,true,true,false)).Status);
    }
}
