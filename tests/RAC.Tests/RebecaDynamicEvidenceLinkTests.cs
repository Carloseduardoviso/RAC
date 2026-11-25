using RAC.Infrastructure;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace RAC.Tests;

public sealed class RebecaDynamicEvidenceLinkTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("wrong-symbol")]
    [InlineData("wrong-message")]
    [InlineData("wrong-task")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("partial")]
    [InlineData("assessment-symbol")]
    [InlineData("assessment-status")]
    [InlineData("assessment-count")]
    [InlineData("assessment-trading")]
    [InlineData("assessment-total")]
    [InlineData("assessment-missing")]
    public void ChecksDynamicReportAgainstDiscovery(string scenario)
    {
        var now=DateTimeOffset.UtcNow;
        var task=Guid.NewGuid();
        var message=Guid.NewGuid();
        var g=Guid.NewGuid();
        var d=new RebecaMarketEvidence(task,message,"daniel",now,JsonSerializer.Serialize(new {
            Mode="RESEARCH_ONLY",TradingAuthorized=false,TaskId=task,ObservedUtc=now,
            CoverageStatus=scenario=="partial"?"PARTIAL_COVERAGE":"RESEARCH_CANDIDATES_AVAILABLE",
            Candidates=new[]{new {Symbol="ABCUSDT",Rank=1,QuoteVolume24h=2000000m,SpreadPercent=0.1m,TradingAuthorized=false}}
        }));
        var report=new RebecaMarketEvidence(g,new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("GABRIEL:REPORT:V2:"+g.ToString("N"))).AsSpan(0,16)),"gabriel",now,
            JsonSerializer.Serialize(new {Schema="GABRIEL_DYNAMIC_V2",Mode="RESEARCH_ONLY",TradingAuthorized=false,IndependentValidation=false,
                Symbols=new[]{scenario=="wrong-symbol"?"XYZUSDT":"ABCUSDT"},Configurations=24,StressRanked=12,
                SourceSha256=new string('A',64),PositiveMean=0,FurtherResearch=0,
                Findings=new[]{new {Status="NON_POSITIVE_NET_RETURN",Count=12}},
                DanielTaskId=scenario=="wrong-task"?Guid.NewGuid():task,
                DanielMessageId=scenario=="wrong-message"?Guid.NewGuid():message,
                DiscoveryAssessments=scenario=="assessment-missing"?[]:new[]{new {
                    Symbol=scenario=="assessment-symbol"?"XYZUSDT":"ABCUSDT",
                    Status=scenario is "assessment-status" or "assessment-total"?"INDEPENDENT_VALIDATION_REQUIRED":"NO_QUALIFYING_CONFIGURATION",
                    StressConfigurations=scenario=="assessment-count"?11:12,
                    FurtherResearch=scenario=="assessment-total"?1:0,
                    TradingAuthorized=scenario=="assessment-trading"
                }}}));
        RebecaMarketEvidence[] sources=scenario=="missing"?[]:scenario=="duplicate"?[d,d]:[d];
        var result=RebecaDynamicEvidenceLink.Review(report,sources);
        Assert.Equal(scenario=="valid"?"INDEPENDENT_VALIDATION_REQUIRED":"INVALID_REPORT",result.Status);
        Assert.False(result.TradingAuthorized);
    }
}
