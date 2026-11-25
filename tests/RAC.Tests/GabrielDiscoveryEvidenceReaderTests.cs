using RAC.Application;
using RAC.Infrastructure;
using System.Text.Json;

namespace RAC.Tests;

public sealed class GabrielDiscoveryEvidenceReaderTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("stale")]
    [InlineData("future")]
    [InlineData("wrong-task")]
    [InlineData("partial")]
    public void RequiresUniqueFreshCompleteEvidence(string scenario)
    {
        var now=DateTimeOffset.UtcNow;
        var task=Guid.NewGuid();
        var observed=scenario=="stale"?now.AddMinutes(-16):scenario=="future"?now.AddSeconds(1):now;
        var content=JsonSerializer.Serialize(new {Mode="RESEARCH_ONLY",TradingAuthorized=false,TaskId=task,ObservedUtc=observed,Candidates=new[]{new DanielRankedCandidate(1,"BTCUSDT",2000000m,0.1m)},CoverageStatus=scenario=="partial"?"PARTIAL_COVERAGE":"RESEARCH_CANDIDATES_AVAILABLE"});
        var row=new RebecaMarketEvidence(task,Guid.NewGuid(),"daniel",observed,content);
        RebecaMarketEvidence[] rows=scenario=="missing"?[]:scenario=="duplicate"?[row,row]:[row];
        var requested=scenario=="wrong-task"?Guid.NewGuid():task;
        if(scenario!="valid")
        {
            Assert.Throws<InvalidOperationException>(()=>GabrielDiscoveryEvidenceReader.Validate(rows,requested,now));
            return;
        }
        var result=GabrielDiscoveryEvidenceReader.Validate(rows,requested,now);
        Assert.Equal(row.MessageId,result.MessageId);
        Assert.Equal("BTCUSDT",Assert.Single(result.Candidates).Symbol);
        Assert.False(result.TradingAuthorized);
    }
}
