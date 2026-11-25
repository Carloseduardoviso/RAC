using RAC.Application;
using RAC.Infrastructure;
using Xunit;
namespace RAC.Tests;
public sealed class RebecaMarketAdvisoryReportTests
{
    [Fact]
    public void CreatesDeterministicResearchReportWithoutTradingPermission()
    {
        var now=DateTimeOffset.UtcNow;
        var task=Guid.NewGuid();
        var a=Guid.NewGuid(); var b=Guid.NewGuid();
        var result=new RebecaMarketAdvisoryResult("CRITICAL_REVIEW",[a,b],["NEWS:BTCUSDT:HIGH_IMPACT_REVIEW","DANIEL:RESEARCH_ONLY"],[]);
        var report=RebecaMarketAdvisoryReport.Create(task,result,now);
        var retry=RebecaMarketAdvisoryReport.Create(task,result with {EvidenceIds=[b,a],Reasons=result.Reasons.Reverse().ToArray()},now);
        Assert.Equal(report,retry);
        Assert.Equal("rebeca",report.Sender);
        Assert.Equal("ana",report.Recipient);
        Assert.Equal(RacMessageKind.Report,report.Kind);
        Assert.Contains("CRITICAL_REVIEW",report.Content);
        Assert.Contains("\"TradingAuthorized\":false",report.Content);
        Assert.Throws<ArgumentException>(()=>RebecaMarketAdvisoryReport.Create(task,result with {EvidenceIds=[]},now));
        Assert.Throws<InvalidOperationException>(()=>RebecaMarketAdvisoryReport.Create(task,result with {EvidenceIds=Enumerable.Range(0,150).Select(_=>Guid.NewGuid()).ToArray()},now));
    }
}
