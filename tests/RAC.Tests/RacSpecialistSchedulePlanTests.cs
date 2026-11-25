using RAC.Application;
namespace RAC.Tests;

public sealed class RacSpecialistSchedulePlanTests
{
    [Fact]
    public void CoversAllSpecialistsWithStableWindowIdentity()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1800000000);
        Assert.Equal(6,RacSpecialistSchedulePlan.Entries.Count);
        Assert.Equal(6,RacSpecialistSchedulePlan.Entries.Select(x=>x.Specialist).Distinct().Count());
        foreach(var schedule in RacSpecialistSchedulePlan.Entries)
        {
            var first=RacSpecialistSchedulePlan.ForWindow(schedule,now);
            Assert.Equal(first,RacSpecialistSchedulePlan.ForWindow(schedule,now.AddSeconds(1)));
            Assert.NotEqual(first.Id,RacSpecialistSchedulePlan.ForWindow(schedule,now.AddMinutes(schedule.IntervalMinutes)).Id);
            Assert.Equal(RacTaskStatus.Pending,first.Status);
        }
    }
}
