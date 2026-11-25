using RAC.Application;
using RAC.Infrastructure;
using System.Text.Json;
using Xunit;
namespace RAC.Tests;
public sealed class RebecaDanielReportReviewTests
{
    [Fact]
    public void ValidatesRankingAndRejectsTampering()
    {
        var task=Guid.NewGuid(); var now=DateTimeOffset.UtcNow;
        var candidates=new[]{new DanielRankedCandidate(1,"BTCUSDT",2000000m,0.1m),new DanielRankedCandidate(2,"ETHUSDT",1000000m,0.2m)};
        string Content(DanielRankedCandidate[] rows)=>JsonSerializer.Serialize(new {Mode="RESEARCH_ONLY",TradingAuthorized=false,TaskId=task,ObservedUtc=now,Candidates=rows});
        var evidence=new RebecaMarketEvidence(task,Guid.NewGuid(),"daniel",now,Content(candidates));
        var result=RebecaDanielReportReview.Review(evidence);
        Assert.Equal("RESEARCH_ONLY",result.Status);
        Assert.Equal(candidates,result.Candidates.ToArray());
        Assert.False(result.TradingAuthorized);
        Assert.Equal("INVALID_REPORT",RebecaDanielReportReview.Review(evidence with {Content=Content(candidates.Reverse().ToArray())}).Status);
        Assert.Equal("INVALID_REPORT",RebecaDanielReportReview.Review(evidence with {Content=evidence.Content.Replace("false","true")}).Status);
        Assert.Equal("INVALID_REPORT",RebecaDanielReportReview.Review(evidence with {TaskId=Guid.NewGuid()}).Status);
        Assert.Equal("NO_CANDIDATES",RebecaDanielReportReview.Review(evidence with {Content=Content([])}).Status);
    }
    [Theory]
    [InlineData("NO_VALID_OBSERVATIONS",false,"NO_CANDIDATES")]
    [InlineData("PARTIAL_COVERAGE",true,"RESEARCH_ONLY")]
    [InlineData("NO_VALID_OBSERVATIONS",true,"INVALID_REPORT")]
    [InlineData("RESEARCH_CANDIDATES_AVAILABLE",false,"INVALID_REPORT")]
    [InlineData("UNKNOWN",false,"INVALID_REPORT")]
    public void ValidatesCoverage(string coverage,bool hasCandidate,string status)
    {
        var task=Guid.NewGuid(); var now=DateTimeOffset.UtcNow;
        DanielRankedCandidate[] rows=hasCandidate?[new(1,"BTCUSDT",2000000m,0.1m)]:[];
        var content=JsonSerializer.Serialize(new {Mode="RESEARCH_ONLY",TradingAuthorized=false,TaskId=task,ObservedUtc=now,Candidates=rows,CoverageStatus=coverage});
        var result=RebecaDanielReportReview.Review(new(task,Guid.NewGuid(),"daniel",now,content));
        Assert.Equal(status,result.Status);
        if(status!="INVALID_REPORT") Assert.Equal(coverage,result.CoverageStatus);
        Assert.False(result.TradingAuthorized);
    }
}
