using RAC.Application;
namespace RAC.Tests;
public sealed class ResearchExperimentExecutionIdentityTests
{
    private static readonly ResearchExperimentPlan.Row Row=ResearchExperimentPlan.Build(["ABCUSDT"])[0];
    private static readonly DateOnly Month=new(2026,5,1);
    private static readonly string Hash=new('A',64);
    [Fact]
    public void StableAcrossDigestCase()
    {
        var id=ResearchExperimentExecutionIdentity.Create(Row,Hash,Month,"REPLAY_V1");
        Assert.Equal(64,id.Length);
        Assert.Equal(id,ResearchExperimentExecutionIdentity.Create(Row,Hash.ToLowerInvariant(),Month,"REPLAY_V1"));
    }
    [Fact]
    public void SeparatesSourceMonthEngineAndConfiguration()
    {
        var ids=new[]{
            ResearchExperimentExecutionIdentity.Create(Row,Hash,Month,"REPLAY_V1"),
            ResearchExperimentExecutionIdentity.Create(Row,new string('B',64),Month,"REPLAY_V1"),
            ResearchExperimentExecutionIdentity.Create(Row,Hash,Month.AddMonths(1),"REPLAY_V1"),
            ResearchExperimentExecutionIdentity.Create(Row,Hash,Month,"REPLAY_V2"),
            ResearchExperimentExecutionIdentity.Create(ResearchExperimentPlan.Build(["ABCUSDT"])[1],Hash,Month,"REPLAY_V1")};
        Assert.Equal(ids.Length,ids.Distinct().Count());
    }
    [Fact]
    public void RejectsMalformedProvenance()
    {
        Assert.Throws<ArgumentException>(()=>ResearchExperimentExecutionIdentity.Create(Row,"invalid",Month,"REPLAY_V1"));
        Assert.Throws<ArgumentException>(()=>ResearchExperimentExecutionIdentity.Create(Row,Hash,Month.AddDays(1),"REPLAY_V1"));
        Assert.Throws<ArgumentException>(()=>ResearchExperimentExecutionIdentity.Create(Row,Hash,Month,"V1|V2"));
    }
}
