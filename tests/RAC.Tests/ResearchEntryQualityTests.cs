using RAC.Application;
namespace RAC.Tests;
public class ResearchEntryQualityTests
{
    [Fact]
    public void UsesOnlyPastMinutes()
    {
        var data=Enumerable.Repeat(new ResearchExperimentReplay.Minute(100,100,1000000),180).Append(new(1,1,0)).ToArray();
        Assert.True(ResearchEntryQuality.Evaluate(data,180,60,100,4,.3m).Allowed);
    }
    [Fact]
    public void RejectsLowLiquidityAndCostMargin()
    {
        var data=Enumerable.Repeat(new ResearchExperimentReplay.Minute(100,100,1000000),180).ToArray();
        Assert.Equal("LIQUIDITY",ResearchEntryQuality.Evaluate(data,180,60,1001,4,.3m).Reason);
        Assert.Equal("COST_MARGIN",ResearchEntryQuality.Evaluate(data,180,60,100,1,.3m).Reason);
    }
}
