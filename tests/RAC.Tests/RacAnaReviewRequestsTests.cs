using RAC.Application;
using Xunit;
namespace RAC.Tests;
public sealed class RacAnaReviewRequestsTests
{
    [Fact]
    public void RequestsAreDeterministicAndAdvisoryOnly()
    {
        var now=DateTimeOffset.UtcNow;
        var result=new RacAnaAdvisoryResult("RISK_REVIEW_REQUIRED",["review"],[Guid.NewGuid()]);
        var first=RacAnaReviewRequests.Create(result,now);
        var retry=RacAnaReviewRequests.Create(result,now.AddMinutes(1));
        Assert.Equal(2,first.Count);
        Assert.Equal(first.Select(x=>x.Id),retry.Select(x=>x.Id));
        Assert.Single(first.Select(x=>x.CorrelationId).Distinct());
        Assert.All(first,x=>{Assert.Equal("ana",x.Sender); Assert.Equal(RacMessageKind.TaskRequest,x.Kind); Assert.Contains("\"TradingAuthorized\":false",x.Content);});
        Assert.Empty(RacAnaReviewRequests.Create(result with {EvidenceIds=[]},now));
    }
}
