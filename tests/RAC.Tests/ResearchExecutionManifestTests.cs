using RAC.Application;
namespace RAC.Tests;
public sealed class ResearchExecutionManifestTests
{
    private static ResearchExecutionManifest.Evidence[] Evidence()=>ResearchExperimentPlan.Build(["ABCUSDT"]).SelectMany(row=>Enumerable.Range(5,6).Select(m=>new ResearchExecutionManifest.Evidence(row,new DateOnly(2026,m,1),new string('A',64)))).ToArray();
    [Fact]
    public void CompleteManifestIsDeterministic()
    {
        var rows=Evidence();
        var first=ResearchExecutionManifest.Create(["ABCUSDT"],"REPLAY_V1",rows);
        Assert.Equal(first,ResearchExecutionManifest.Create(["ABCUSDT"],"REPLAY_V1",rows.Reverse().ToArray()));
        Assert.Equal(144,first.Executions);
        Assert.False(first.TradingAuthorized);
    }
    [Fact]
    public void RejectsPartialDuplicateAndConflictingSource()
    {
        var rows=Evidence();
        Assert.Throws<ArgumentException>(()=>ResearchExecutionManifest.Create(["ABCUSDT"],"REPLAY_V1",rows.Skip(1).ToArray()));
        rows[0]=rows[1];
        Assert.Throws<ArgumentException>(()=>ResearchExecutionManifest.Create(["ABCUSDT"],"REPLAY_V1",rows));
        rows=Evidence();
        rows[0]=rows[0] with {SourceSha256=new string('B',64)};
        Assert.Throws<ArgumentException>(()=>ResearchExecutionManifest.Create(["ABCUSDT"],"REPLAY_V1",rows));
    }

    [Fact]
    public void TwentyAssetManifestRequiresAll2880Executions()
    {
        var symbols=Enumerable.Range(0,20).Select(i=>$"SYN{i:D2}USDT").ToArray();
        var rows=ResearchExperimentPlan.Build(symbols).SelectMany(row=>Enumerable.Range(5,6).Select(m=>new ResearchExecutionManifest.Evidence(row,new DateOnly(2026,m,1),new string('A',64)))).ToArray();
        var manifest=ResearchExecutionManifest.Create(symbols,"REPLAY_V1",rows);
        Assert.Equal(2880,manifest.Executions);
        Assert.Equal(20,manifest.Assets);
        Assert.Throws<ArgumentException>(()=>ResearchExecutionManifest.Create(symbols,"REPLAY_V1",rows.Skip(1).ToArray()));
    }
}
