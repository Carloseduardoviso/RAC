using RAC.Application;
namespace RAC.Tests;
public class ResearchExperimentPlanTests
{
    [Fact]
    public void SupportsExplicitResearchSymbolsWithoutChangingLegacyPlan()
    {
        var dynamicPlan=ResearchExperimentPlan.Build(new[]{"ABCUSDT","XYZUSDT"});
        Assert.Equal(48,dynamicPlan.Count);
        Assert.Equal(2,dynamicPlan.Select(x=>x.Symbol).Distinct().Count());
        Assert.All(dynamicPlan,x=>Assert.False(x.Parameters.TradingAuthorized));
        Assert.Equal(288,ResearchExperimentPlan.Build().Count);
        Assert.Throws<ArgumentException>(()=>ResearchExperimentPlan.Build([]));
        Assert.Throws<ArgumentException>(()=>ResearchExperimentPlan.Build(new[]{"ABCUSDT","ABCUSDT"}));
        Assert.Throws<ArgumentException>(()=>ResearchExperimentPlan.Build(new[]{"abcusdt"}));
        Assert.Throws<ArgumentException>(()=>ResearchExperimentPlan.Build(new[]{"BTCUSDC"}));
        Assert.Throws<ArgumentException>(()=>ResearchExperimentPlan.Build(Enumerable.Repeat("ABCUSDT",21)));
    }
    [Fact]
    public void PlanContains288UniqueResearchOnlyRows()
    {
        var plan=ResearchExperimentPlan.Build();
        Assert.Equal(288,plan.Count);
        Assert.Equal(288,plan.Distinct().Count());
        Assert.Equal(12,plan.Select(x=>x.Symbol).Distinct().Count());
        Assert.All(plan,x=>Assert.False(x.Parameters.TradingAuthorized));
    }
}
