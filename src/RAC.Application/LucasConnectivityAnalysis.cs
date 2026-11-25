namespace RAC.Application;
public sealed record LucasEndpointHealth(string Exchange,string Environment,LucasExchangeHealth Health);
public sealed record LucasConnectivitySnapshot(DateTimeOffset ObservedUtc,string Status,IReadOnlyList<LucasEndpointHealth> Endpoints)
{
    public bool TradingAuthorized=>false;
    public bool HumanReviewRequired=>Status!="PUBLIC_HEALTHY";
}
public static class LucasConnectivityAnalysis
{
    public static LucasConnectivitySnapshot Evaluate(
        IReadOnlyList<(string Exchange,string Environment)> expected,
        IReadOnlyList<LucasExchangeObservation> observations,DateTimeOffset now,
        TimeSpan maximumAge,TimeSpan maximumLatency,TimeSpan maximumClockOffset)
    {
        if(expected.Count is <1 or >20 || expected.Distinct().Count()!=expected.Count ||
            observations.Count>20 || maximumAge<=TimeSpan.Zero || maximumLatency<=TimeSpan.Zero ||
            maximumClockOffset<=TimeSpan.Zero)
            throw new ArgumentException("Invalid connectivity assessment.");
        var map=new Dictionary<(string,string),LucasExchangeObservation>();
        foreach(var observation in observations)
        {
            var key=(observation.Exchange,observation.Environment);
            if(!expected.Contains(key) || !map.TryAdd(key,observation))
                throw new ArgumentException("Unexpected or duplicate observation.");
        }
        var results=new List<LucasEndpointHealth>();
        foreach(var key in expected)
        {
            if(string.IsNullOrWhiteSpace(key.Exchange) || key.Environment is not ("PUBLIC" or "TESTNET"))
                throw new ArgumentException("Invalid expected endpoint.");
            var health=map.TryGetValue(key,out var observation)
                ? LucasExchangeHealthAnalysis.Evaluate(observation,now,maximumAge,maximumLatency,maximumClockOffset)
                : new LucasExchangeHealth("REVIEW_REQUIRED",0,null,Array.AsReadOnly(new[]{"MISSING_OBSERVATION"}));
            results.Add(new(key.Exchange,key.Environment,health));
        }
        var status=results.Any(x=>x.Health.Status=="RATE_LIMITED")?"RATE_LIMITED":
            results.All(x=>x.Health.Status=="PUBLIC_HEALTHY")?"PUBLIC_HEALTHY":"REVIEW_REQUIRED";
        return new(now,status,results.AsReadOnly());
    }
}
