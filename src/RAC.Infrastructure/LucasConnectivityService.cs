using RAC.Application;
namespace RAC.Infrastructure;
public sealed class LucasConnectivityService(LucasExchangeProbeRegistry registry,TimeSpan maximumAge,
    TimeSpan maximumLatency,TimeSpan maximumClockOffset)
{
    public async Task<LucasConnectivitySnapshot> AssessAsync(CancellationToken ct)
    {
        var expected=registry.Available;
        var observations=new List<LucasExchangeObservation>();
        foreach(var key in expected)
        {
            if(observations.Count>0) await Task.Delay(TimeSpan.FromSeconds(2),ct);
            observations.Add(await registry.ReadAsync(key.Exchange,key.Environment,ct));
        }
        return LucasConnectivityAnalysis.Evaluate(expected,observations,DateTimeOffset.UtcNow,
            maximumAge,maximumLatency,maximumClockOffset);
    }
}
