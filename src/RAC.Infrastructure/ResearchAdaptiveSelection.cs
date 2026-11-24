namespace RAC.Infrastructure;
public static class ResearchAdaptiveSelection
{
    public static string Choose(DateTimeOffset now,IReadOnlyList<ResearchProductivity.Metric> metrics)
    {
        var baseline=ResearchTopicRotation.ForDate(now);
        var day=now.UtcDateTime.Date.Ticks/TimeSpan.TicksPerDay;
        // Only one day in eight is adaptive; seven consecutive days retain full exploration.
        if(day%8!=0)return baseline;
        var allowed=Enumerable.Range(0,7).Select(i=>ResearchTopicRotation.ForDate(now.AddDays(i))).ToHashSet(StringComparer.Ordinal);
        var best=metrics.Where(x=>allowed.Contains(x.Topic)&&x.Completed>=3&&x.Attempts>=x.Completed&&x.Found>=20&&x.Saved>=0&&x.Saved<=x.Found&&x.Completed*1.0/x.Attempts>=0.75)
            .OrderByDescending(x=>(double)x.Saved/x.Attempts)
            .ThenByDescending(x=>(double)x.Saved/x.Found)
            .ThenBy(x=>x.Topic,StringComparer.Ordinal).FirstOrDefault();
        return best is {Saved:>0}?best.Topic:baseline;
    }
}
