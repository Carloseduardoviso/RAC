namespace RAC.Application;

public sealed record RacSpecialistSchedule(string Leader,string Specialist,string Activity,int IntervalMinutes);

public static class RacSpecialistSchedulePlan
{
    public static IReadOnlyList<RacSpecialistSchedule> Entries { get; } = Array.AsReadOnly(new[]
    {
        new RacSpecialistSchedule("rebeca","daniel","MARKET_DISCOVERY",60),
        new RacSpecialistSchedule("rebeca","gabriel","QUANTITATIVE_RESEARCH",60),
        new RacSpecialistSchedule("carlos","rafael","CAPITAL_ANALYSIS",15),
        new RacSpecialistSchedule("carlos","samuel","LIQUIDITY_COST_ANALYSIS",15),
        new RacSpecialistSchedule("carlos","miguel","AUDIT",60),
        new RacSpecialistSchedule("ana","lucas","EXCHANGE_HEALTH",1)
    });

    public static RacAgentTask ForWindow(RacSpecialistSchedule schedule,DateTimeOffset now)
    {
        if (!Entries.Contains(schedule)) throw new ArgumentException("Unknown schedule.");
        var seconds = schedule.IntervalMinutes * 60L;
        var epoch = now.ToUnixTimeSeconds();
        var window = epoch - ((epoch % seconds + seconds) % seconds);
        var start = DateTimeOffset.FromUnixTimeSeconds(window);
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(FormattableString.Invariant($"RAC:SCHEDULE:V1:{schedule.Specialist}:{window}")));
        var task = RacTaskLifecycle.Create(schedule.Leader,schedule.Specialist,schedule.Activity,start);
        return task with { Id = new Guid(bytes.AsSpan(0,16)) };
    }
}
