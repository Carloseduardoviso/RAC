using RAC.Application;

namespace RAC.Tests;

public sealed class PaperPerformanceMathTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(1, 4, 25)]
    [InlineData(3, 4, 75)]
    [InlineData(3, 3, 100)]
    [InlineData(9, 3, 100)]
    public void Profitability_uses_all_closed_sells_as_denominator(int profitable, int closed, int rate)
    {
        Assert.Equal((decimal)rate, PaperPerformanceMath.WinRatePercent(profitable, closed));
    }

    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(25, 100, 25)]
    [InlineData(150, 100, 150)]
    [InlineData(10, 0, 0)]
    [InlineData(-10, 100, 0)]
    public void Exposure_is_marked_to_market_and_not_forcibly_capped(decimal open, decimal equity, decimal expected)
    {
        Assert.Equal(expected, PaperPerformanceMath.ExposurePercent(open, equity));
    }

    [Fact]
    public void Contributions_are_not_counted_as_trading_profit()
    {
        Assert.Equal(-5m, PaperPerformanceMath.NetResultBrl(195m, 200m));
        Assert.Equal(15m, PaperPerformanceMath.NetResultBrl(115m, 100m));
    }
}
