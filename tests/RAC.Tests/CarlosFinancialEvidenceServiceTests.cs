using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class CarlosFinancialEvidenceServiceTests
{
    [Fact]
    public void PrioritizesRiskAndRetainsAllEvidence()
    {
        var r=Guid.NewGuid(); var s=Guid.NewGuid(); var m=Guid.NewGuid();
        var result=CarlosFinancialEvidenceService.Summarize([new(r,null,"RISK_LOCKED")],
            [new(s,"BTCUSDT",[],"REVIEW_REQUIRED")],[new(m,"CRITICAL_REVIEW",[])]);
        Assert.Equal("RISK_LOCKED",result.Status);
        Assert.Equal(3,result.EvidenceIds.Count);
        Assert.Contains("MIGUEL:CRITICAL_REVIEW",result.Reasons);
        Assert.False(result.TradingAuthorized);
    }
    [Fact]
    public void MissingEvidenceNeverAuthorizesTrading()
    {
        var result=CarlosFinancialEvidenceService.Summarize([],[],[]);
        Assert.Equal("REVIEW_REQUIRED",result.Status);
        Assert.Equal(3,result.Reasons.Count);
        Assert.False(result.TradingAuthorized);
    }
}
