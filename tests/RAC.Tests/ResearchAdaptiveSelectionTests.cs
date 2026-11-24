using RAC.Infrastructure;
namespace RAC.Tests;
public class ResearchAdaptiveSelectionTests
{
    private static DateTimeOffset AdaptiveDay()
    {
        var now=new DateTimeOffset(2026,11,24,0,0,0,TimeSpan.Zero);
        while(now.UtcDateTime.Date.Ticks/TimeSpan.TicksPerDay%8!=0)now=now.AddDays(1);
        return now;
    }
    [Fact]
    public void PrefersProductiveKnownTopicWithEnoughEvidence()
    {
        var now=AdaptiveDay();
        var topic=ResearchTopicRotation.ForDate(now.AddDays(1));
        Assert.Equal(topic,ResearchAdaptiveSelection.Choose(now,[new(topic,4,4,0,80,40,.5,100)]));
    }
    [Fact]
    public void PreservesSevenExplorationDaysAndFallsBackWithoutEvidence()
    {
        var now=AdaptiveDay();
        Assert.Equal(ResearchTopicRotation.ForDate(now),ResearchAdaptiveSelection.Choose(now,[]));
        var topic=ResearchTopicRotation.ForDate(now.AddDays(1));
        for(int i=1;i<=7;i++)
            Assert.Equal(ResearchTopicRotation.ForDate(now.AddDays(i)),ResearchAdaptiveSelection.Choose(now.AddDays(i),[new(topic,4,4,0,80,40,.5,100)]));
        Assert.Equal(ResearchTopicRotation.ForDate(now),ResearchAdaptiveSelection.Choose(now,[new("unknown",4,4,0,80,80,1,100),new(topic,1,1,0,20,20,1,100)]));
    }
}
