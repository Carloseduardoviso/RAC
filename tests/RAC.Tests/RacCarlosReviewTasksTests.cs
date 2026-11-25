using RAC.Application;
using Xunit;
namespace RAC.Tests;
public sealed class RacCarlosReviewTasksTests
{
    [Fact]
    public void PlanIsStableAndRestrictedToFinancialSpecialists()
    {
        var id=Guid.NewGuid(); var now=DateTimeOffset.UtcNow;
        var tasks=RacCarlosReviewTasks.Create(id,now);
        Assert.Equal(new[]{"rafael","samuel","miguel"},tasks.Select(x=>x.Specialist));
        Assert.Equal(new[]{"CAPITAL_ANALYSIS","LIQUIDITY_COST_ANALYSIS","AUDIT"},tasks.Select(x=>x.Description));
        Assert.All(tasks,x=>{Assert.Equal("carlos",x.Leader);Assert.Equal(RacTaskStatus.Pending,x.Status);});
        Assert.Equal(tasks.Select(x=>x.Id),RacCarlosReviewTasks.Create(id,now.AddMinutes(1)).Select(x=>x.Id));
        Assert.Equal(3,tasks.Select(x=>x.Id).Distinct().Count());
        Assert.Empty(tasks.Select(x=>x.Id).Intersect(RacCarlosReviewTasks.Create(Guid.NewGuid(),now).Select(x=>x.Id)));
        Assert.Throws<ArgumentException>(()=>RacCarlosReviewTasks.Create(Guid.Empty,now));
    }
}
