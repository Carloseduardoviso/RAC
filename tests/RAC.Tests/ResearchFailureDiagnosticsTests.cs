using RAC.Application;
namespace RAC.Tests;
public class ResearchFailureDiagnosticsTests
{
    [Fact]
    public void CountsOverlappingReasonsWithoutDuplicatingPerDecision()
    {
        var d=ResearchLeadershipReview.Evaluate(new(-1,-1,1,10,4,1));
        var findings=ResearchFailureDiagnostics.Summarize(new[]{d,d});
        Assert.Equal(5,findings.Count);
        Assert.All(findings,x=>Assert.Equal(2,x.Count));
    }
}
