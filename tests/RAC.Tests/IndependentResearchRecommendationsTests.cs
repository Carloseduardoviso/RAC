using RAC.Application;
namespace RAC.Tests;
public sealed class IndependentResearchRecommendationsTests
{
    [Fact]
    public void RecommendationsRequireEvidenceAndNeverAuthorizeExecution()
    {
        var end=new DateTimeOffset(2027,6,1,0,0,0,TimeSpan.Zero);
        var rows=Enumerable.Range(0,6).SelectMany(i=>new[]{.0015m,.003m}.Select(c=>new IndependentResearchGate.Evidence(new DateOnly(2026,12,1).AddMonths(i),c,6,30,10,1,false))).ToArray();
        Assert.Contains("COLLECT_COMPLETE_INDEPENDENT_EVIDENCE",IndependentResearchRecommendations.Evaluate([],end).Actions);
        var valid=IndependentResearchRecommendations.Evaluate(rows,end);
        Assert.Contains("REQUEST_ANA_HUMAN_REVIEW",valid.Actions);
        Assert.False(valid.TradingAuthorized);
        rows[0]=rows[0] with {Unresolved=true};
        var risk=IndependentResearchRecommendations.Evaluate(rows,end);
        Assert.Contains("REQUEST_CARLOS_RISK_REVIEW",risk.Actions);
        Assert.DoesNotContain("REQUEST_ANA_HUMAN_REVIEW",risk.Actions);
        rows[0]=rows[0] with {Profit=-1};
        Assert.Equal(new[]{"AUDIT_EVIDENCE_INTEGRITY"},IndependentResearchRecommendations.Evaluate(rows,end).Actions.ToArray());
    }
}
