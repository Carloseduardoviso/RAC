using RAC.Infrastructure;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using Xunit;
namespace RAC.Tests;
public sealed class RebecaGabrielReportReviewTests
{
    [Fact]
    public void ValidatesSummaryAndRejectsInconsistentCounts()
    {
        var task=Guid.NewGuid();
        var id=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("GABRIEL:REPORT:V1:"+task.ToString("N"))).AsSpan(0,16));
        string Content(int count,int further,bool authorized=false)=>JsonSerializer.Serialize(new {
            Mode="RESEARCH_ONLY",TradingAuthorized=authorized,IndependentValidation=false,
            SourceSha256=new string('A',64),Configurations=288,StressRanked=144,PositiveMean=20,FurtherResearch=further,
            Findings=new[]{new {Status="INDEPENDENT_VALIDATION_REQUIRED",Count=10},new {Status="NON_POSITIVE_NET_RETURN",Count=count}}});
        var e=new RebecaMarketEvidence(task,id,"gabriel",DateTimeOffset.UtcNow,Content(134,10));
        var result=RebecaGabrielReportReview.Review(e);
        Assert.Equal("INDEPENDENT_VALIDATION_REQUIRED",result.Status);
        Assert.Equal(10,result.FurtherResearch);
        Assert.False(result.TradingAuthorized);
        Assert.Equal("INVALID_REPORT",RebecaGabrielReportReview.Review(e with {Content=Content(133,10)}).Status);
        Assert.Equal("INVALID_REPORT",RebecaGabrielReportReview.Review(e with {Content=Content(134,11)}).Status);
        Assert.Equal("INVALID_REPORT",RebecaGabrielReportReview.Review(e with {Content=Content(134,10,true)}).Status);
        Assert.Equal("INVALID_REPORT",RebecaGabrielReportReview.Review(e with {MessageId=Guid.NewGuid()}).Status);
    }
}
