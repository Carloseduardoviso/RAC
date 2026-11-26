using RAC.Application;
namespace RAC.Tests;
public sealed class IndependentResearchClassificationTests
{
    [Fact]
    public void ClassificationNeverAuthorizesTrading()
    {
        var end=new DateTimeOffset(2027,6,1,0,0,0,TimeSpan.Zero);
        var rows=Enumerable.Range(0,6).SelectMany(i=>new[]{.0015m,.003m}.Select(c=>new IndependentResearchGate.Evidence(new DateOnly(2026,12,1).AddMonths(i),c,6,30,10,1,false))).ToArray();
        var eligible=IndependentResearchClassification.Evaluate(rows,end);
        Assert.Equal("HUMAN_REVIEW_REQUIRED",eligible.Status);
        Assert.False(eligible.TradingAuthorized);
        Assert.Equal("VALIDATION_PENDING",IndependentResearchClassification.Evaluate(rows,end.AddDays(-1)).Status);
        Assert.Equal("VALIDATION_PENDING",IndependentResearchClassification.Evaluate(rows.Skip(1).ToArray(),end).Status);
        rows[0]=rows[0] with {Unresolved=true};
        Assert.Equal("RESEARCH_REJECTED",IndependentResearchClassification.Evaluate(rows,end).Status);
        rows[0]=rows[0] with {Profit=-1};
        Assert.Equal("INVALID_EVIDENCE",IndependentResearchClassification.Evaluate(rows,end).Status);
    }
}
