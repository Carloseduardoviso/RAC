namespace RAC.Application;

/// <summary>Pure analytics over two equal, non-overlapping UTC rolling windows.</summary>
public static class PaperHistoricalComparison
{
    public static (DateTimeOffset PreviousStart, DateTimeOffset CurrentStart, DateTimeOffset End)
        Windows(DateTimeOffset now, int days = 7)
    {
        if (days is < 1 or > 30) throw new ArgumentOutOfRangeException(nameof(days));
        return (now.AddDays(-2 * days), now.AddDays(-days), now);
    }

    /// <summary>Null when percentage is undefined (previous window was zero).</summary>
    public static decimal? PercentChange(decimal previous, decimal current) =>
        previous == 0m ? null : (current - previous) / Math.Abs(previous) * 100m;
}
