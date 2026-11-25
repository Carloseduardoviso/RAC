using RAC.Application;
namespace RAC.Tests;
public sealed class LucasExchangeHealthAnalysisTests
{
    [Fact]
    public void ClassifiesPublicHealthWithoutAuthorizingTrading()
    {
        var now=DateTimeOffset.UtcNow;
        var observation=new LucasExchangeObservation("BINANCE","PUBLIC",now.AddMilliseconds(-100),now,now.AddMilliseconds(-50),200);
        LucasExchangeHealth Evaluate(LucasExchangeObservation x)=>LucasExchangeHealthAnalysis.Evaluate(x,now,TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(1),TimeSpan.FromMilliseconds(500));
        var healthy=Evaluate(observation);
        Assert.Equal("PUBLIC_HEALTHY",healthy.Status);
        Assert.Equal(100m,healthy.LatencyMilliseconds);
        Assert.Equal(0m,healthy.ClockOffsetMilliseconds);
        Assert.False(healthy.TradingAuthorized);
        Assert.Equal("RATE_LIMITED",Evaluate(observation with {HttpStatus=429}).Status);
        Assert.Contains("UNAVAILABLE",Evaluate(observation with {HttpStatus=null,ServerUtc=null}).Findings);
        Assert.Contains("CLOCK_OFFSET_OR_UNCERTAINTY",Evaluate(observation with {ServerUtc=now.AddSeconds(2)}).Findings);
        Assert.Contains("HIGH_LATENCY",Evaluate(observation with {StartedUtc=now.AddSeconds(-2)}).Findings);
        Assert.Contains("INVALID_OBSERVATION_AGE",Evaluate(observation with {StartedUtc=now.AddMinutes(-2),FinishedUtc=now.AddMinutes(-1)}).Findings);
        Assert.Throws<ArgumentException>(()=>Evaluate(observation with {Environment="LIVE"}));
    }
}
