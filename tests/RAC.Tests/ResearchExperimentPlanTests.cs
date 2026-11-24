using RAC.Application;
namespace RAC.Tests;
public class ResearchExperimentPlanTests
{
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
