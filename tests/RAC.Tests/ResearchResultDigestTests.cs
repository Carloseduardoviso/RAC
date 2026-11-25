using RAC.Application;
namespace RAC.Tests;
public sealed class ResearchResultDigestTests
{
    [Fact]
    public void DigestBindsExecutionAndEveryResultField()
    {
        var id=new string('A',64);
        var result=new ResearchExperimentReplay.Result(2,1,3,2,2,1,false);
        var hash=ResearchResultDigest.Create(id,result);
        Assert.Equal(64,hash.Length);
        Assert.Equal(hash,ResearchResultDigest.Create(id.ToLowerInvariant(),result with {NetPct=2.00m}));
        Assert.NotEqual(hash,ResearchResultDigest.Create(new string('B',64),result));
        foreach(var changed in new[]{result with {NetPct=3},result with {DrawdownPct=2},result with {Trades=4},result with {Wins=1},result with {ProfitFactor=null},result with {Costs=2},result with {Unresolved=true}})
            Assert.NotEqual(hash,ResearchResultDigest.Create(id,changed));
        Assert.Throws<ArgumentException>(()=>ResearchResultDigest.Create("invalid",result));
    }
}
