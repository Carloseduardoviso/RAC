namespace RAC.Application;
public sealed record LucasExchangeObservation(string Exchange,string Environment,
    DateTimeOffset StartedUtc,DateTimeOffset FinishedUtc,DateTimeOffset? ServerUtc,int? HttpStatus);
public sealed record LucasExchangeHealth(string Status,decimal LatencyMilliseconds,
    decimal? ClockOffsetMilliseconds,IReadOnlyList<string> Findings)
{
    public bool TradingAuthorized=>false;
}
public static class LucasExchangeHealthAnalysis
{
    public static LucasExchangeHealth Evaluate(LucasExchangeObservation observation,
        DateTimeOffset now,TimeSpan maximumAge,TimeSpan maximumLatency,TimeSpan maximumClockOffset)
    {
        if(maximumAge<=TimeSpan.Zero || maximumLatency<=TimeSpan.Zero || maximumClockOffset<=TimeSpan.Zero ||
            string.IsNullOrWhiteSpace(observation.Exchange) || observation.Environment is not ("PUBLIC" or "TESTNET") ||
            observation.FinishedUtc<observation.StartedUtc ||
            observation.HttpStatus is <100 or >599)
            throw new ArgumentException("Invalid exchange observation.");
        var findings=new List<string>();
        var duration=observation.FinishedUtc-observation.StartedUtc;
        if(observation.FinishedUtc>now || now-observation.FinishedUtc>maximumAge)
            findings.Add("INVALID_OBSERVATION_AGE");
        if(observation.HttpStatus is 429 or 418) findings.Add("RATE_LIMITED");
        else if(observation.HttpStatus is null or <200 or >=300) findings.Add("UNAVAILABLE");
        if(duration>maximumLatency) findings.Add("HIGH_LATENCY");
        decimal? offset=null;
        if(observation.ServerUtc is DateTimeOffset server)
        {
            var midpoint=observation.StartedUtc+TimeSpan.FromTicks(duration.Ticks/2);
            offset=(decimal)(server-midpoint).TotalMilliseconds;
            if(Math.Abs(offset.Value)+(decimal)duration.TotalMilliseconds/2>(decimal)maximumClockOffset.TotalMilliseconds)
                findings.Add("CLOCK_OFFSET_OR_UNCERTAINTY");
        }
        else findings.Add("SERVER_TIME_UNAVAILABLE");
        var status=findings.Count==0?"PUBLIC_HEALTHY":findings.Contains("RATE_LIMITED")?"RATE_LIMITED":"REVIEW_REQUIRED";
        return new(status,(decimal)duration.TotalMilliseconds,offset,findings.AsReadOnly());
    }
}
