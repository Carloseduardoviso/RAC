using RAC.Application;

namespace RAC.Tests;

public sealed class PaperRiskDiagnosticsTests
{
    [Theory]
    [InlineData(100, 100, 0)]
    [InlineData(98.9975, 100, 1.0025)]
    [InlineData(101, 100, 0)]
    [InlineData(0, 100, 100)]
    public void Daily_loss_reports_only_actual_decline(decimal equity, decimal start, decimal expected)
        => Assert.Equal(expected, PaperRiskDiagnostics.DailyLossPercent(equity, start));

    [Fact]
    public void Invalid_reference_does_not_invent_a_daily_loss()
    {
        Assert.Null(PaperRiskDiagnostics.DailyLossPercent(90m, 0m));
        Assert.Null(PaperRiskDiagnostics.DailyLossPercent(90m, -1m));
    }

    [Fact]
    public void Closed_sell_loss_rate_is_undefined_without_sells()
    {
        Assert.Null(PaperRiskDiagnostics.LossRatePercent(0, 0));
        Assert.Equal(75m, PaperRiskDiagnostics.LossRatePercent(3, 4));
        Assert.Equal(100m, PaperRiskDiagnostics.LossRatePercent(5, 4));
        Assert.Equal(0m, PaperRiskDiagnostics.LossRatePercent(-1, 4));
    }
}
