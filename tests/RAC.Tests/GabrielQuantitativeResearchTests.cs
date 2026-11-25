using RAC.Application;
namespace RAC.Tests;

public sealed class GabrielQuantitativeResearchTests
{
    [Fact]
    public void PositiveHistoricalReturnsNeverAuthorizeTrading()
    {
        var result=GabrielQuantitativeResearch.Evaluate(Enumerable.Repeat(1m,30).ToArray());
        Assert.Equal("INDEPENDENT_VALIDATION_REQUIRED",result.Status);
        Assert.Equal(1m,result.MeanReturnPercent);
        Assert.Equal(100m,result.WinRatePercent);
        Assert.False(result.TradingAuthorized);
        Assert.Equal("INSUFFICIENT_SAMPLE",GabrielQuantitativeResearch.Evaluate(new[]{1m}).Status);
        Assert.Equal("NON_POSITIVE_NET_RETURN",GabrielQuantitativeResearch.Evaluate(Enumerable.Repeat(-1m,30).ToArray()).Status);
        Assert.Equal("NO_EVIDENCE",GabrielQuantitativeResearch.Evaluate(Array.Empty<decimal>()).Status);
    }
}
