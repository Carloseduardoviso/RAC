using RAC.Application;
using Xunit;

namespace RAC.Tests;
public sealed class RacAnaEvidenceBatchTests
{
    [Fact]
    public void PreservesSeparateTaskEvidenceAndRiskNotices()
    {
        var now = DateTimeOffset.UtcNow;
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var rows = new[] {
            RacAgentCommunication.Create("rebeca","ana",RacMessageKind.Report,"market",first,now),
            RacAgentCommunication.Create("carlos","ana",RacMessageKind.RiskNotice,"stop",second,now),
            RacAgentCommunication.Create("lucas","ana",RacMessageKind.Report,"old",second,now.AddHours(-1))
        };
        var result = RacAnaEvidenceBatch.Review(new[]{first,second,first},rows,now,TimeSpan.FromMinutes(5));
        Assert.Equal(2,result.Count);
        Assert.Single(result.Single(x=>x.CorrelationId==first).Evidence);
        Assert.Empty(result.Single(x=>x.CorrelationId==second).Evidence);
        Assert.True(result.Single(x=>x.CorrelationId==second).RiskNoticePresent);
        Assert.False(result.Single(x=>x.CorrelationId==first).RiskNoticePresent);
    }
}
