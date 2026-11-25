using RAC.Application;
namespace RAC.Tests;
public sealed class LucasConnectivityAnalysisTests
{
    [Fact]
    public void ConsolidatesEndpointsAndFailsClosedOnMissingOrLimitedObservations()
    {
        var now=DateTimeOffset.UtcNow;
        var expected=new[]{("BINANCE","PUBLIC"),("BINANCE","TESTNET")};
        var spot=new LucasExchangeObservation("BINANCE","PUBLIC",now.AddMilliseconds(-100),now,now.AddMilliseconds(-50),200);
        var testnet=spot with {Environment="TESTNET"};
        LucasConnectivitySnapshot Evaluate(params LucasExchangeObservation[] observations)=>
            LucasConnectivityAnalysis.Evaluate(expected,observations,now,TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(1),TimeSpan.FromMilliseconds(500));
        var healthy=Evaluate(spot,testnet);
        Assert.Equal("PUBLIC_HEALTHY",healthy.Status);
        Assert.False(healthy.TradingAuthorized);
        Assert.False(healthy.HumanReviewRequired);
        var missing=Evaluate(spot);
        Assert.True(missing.HumanReviewRequired);
        Assert.Contains("MISSING_OBSERVATION",missing.Endpoints[1].Health.Findings);
        Assert.Equal("RATE_LIMITED",Evaluate(spot,testnet with {HttpStatus=429}).Status);
        Assert.Equal("REVIEW_REQUIRED",Evaluate(spot,testnet with {HttpStatus=503}).Status);
        Assert.Throws<ArgumentException>(()=>Evaluate(spot,spot));
        Assert.Throws<ArgumentException>(()=>Evaluate(spot with {Exchange="OTHER"}));
    }
}
