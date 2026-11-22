using RAC.Application;

namespace RAC.Tests;

public sealed class PaperHistoricalComparisonTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Rolling_windows_are_seven_days_each_and_non_overlapping()
    {
        var (previousStart, currentStart, end) = PaperHistoricalComparison.Windows(Now);
        Assert.Equal(Now, end);
        Assert.Equal(TimeSpan.FromDays(7), currentStart - previousStart);
        Assert.Equal(TimeSpan.FromDays(7), end - currentStart);
        Assert.Throws<ArgumentOutOfRangeException>(() => PaperHistoricalComparison.Windows(Now, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => PaperHistoricalComparison.Windows(Now, 31));
    }

    [Theory]
    [InlineData(0, 10, null)]
    [InlineData(5, 10, 100)]
    [InlineData(10, 5, -50)]
    [InlineData(-10, -5, 50)]
    [InlineData(-10, -15, -50)]
    public void Percent_change_is_explicitly_undefined_with_zero_baseline(decimal previous, decimal current, int? expected)
    {
        Assert.Equal(expected is null ? null : (decimal?)expected,
            PaperHistoricalComparison.PercentChange(previous, current));
    }
}
