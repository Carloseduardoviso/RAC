using RAC.Application;
using Xunit;
namespace RAC.Tests;
public class IndependentResearchFreezeTests
{
    [Fact]
    public void FreezeBindsConfigurationManifestAndUtcTime()
    {
        var plan=ResearchExperimentPlan.Build(["ABCUSDT"]);
        var now=new DateTimeOffset(2026,11,25,0,0,0,TimeSpan.Zero);
        var engine=ResearchEngineCatalog.Current.Id;
        var hash=new string('A',64);
        var id=IndependentResearchFreeze.Create(plan[0],engine,hash,now);
        Assert.Equal(id,IndependentResearchFreeze.Create(plan[0],engine,hash.ToLowerInvariant(),now.ToOffset(TimeSpan.FromHours(-4))));
        Assert.NotEqual(id,IndependentResearchFreeze.Create(plan[1],engine,hash,now));
        Assert.NotEqual(id,IndependentResearchFreeze.Create(plan[0],engine,new string('B',64),now));
        Assert.Throws<ArgumentException>(()=>IndependentResearchFreeze.Create(plan[0],engine,hash,IndependentResearchFreeze.ValidationStart));
        Assert.Throws<ArgumentException>(()=>IndependentResearchFreeze.Create(plan[0],engine,hash,now.AddMonths(-1)));
        Assert.Throws<ArgumentException>(()=>IndependentResearchFreeze.Create(plan[0],engine,"bad",now));
    }
}
