using RAC.Application;
namespace RAC.Tests;
public sealed class IndependentResearchDeteriorationTests
{
    [Fact]
    public void FlagsConsecutiveLossesAndRiskWithoutAuthorizingTrading()
    {
        var a=new IndependentResearchGate.Evidence(new DateOnly(2027,1,1),.003m,10,5,10,2,false);
        var b=a with {Month=a.Month.AddMonths(1)};
        var result=IndependentResearchDeterioration.Evaluate([a,b]);
        Assert.Equal("DETERIORATION_REVIEW_REQUIRED",result.Status);
        Assert.Contains("CONSECUTIVE_NEGATIVE_MONTHS",result.Reasons);
        Assert.False(result.TradingAuthorized);
        Assert.Equal("NO_ALERT",IndependentResearchDeterioration.Evaluate([a,b with {Month=b.Month.AddMonths(1)}]).Status);
        Assert.Equal("INVALID_EVIDENCE",IndependentResearchDeterioration.Evaluate([a,a]).Status);
        Assert.Contains("RISK_REVIEW_REQUIRED",IndependentResearchDeterioration.Evaluate([a with {Unresolved=true}]).Reasons);
        Assert.Equal("INSUFFICIENT_EVIDENCE",IndependentResearchDeterioration.Evaluate([]).Status);
    }
}
