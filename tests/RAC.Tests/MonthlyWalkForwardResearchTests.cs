using System.IO.Compression;
using System.Text;
using RAC.Application;

namespace RAC.Tests;

public sealed class MonthlyWalkForwardResearchTests
{
    private static byte[] Month(bool gap = false)
    {
        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("BTCUSDT-1m-2026-10.csv", CompressionLevel.Fastest);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            var origin = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
            for (var i = 0; i < 31 * 1440; i++)
            {
                var micros = origin.AddMinutes(i + (gap && i >= 1440 ? 1 : 0))
                    .ToUnixTimeMilliseconds() * 1000L;
                writer.WriteLine($"{micros},100,101,99,100,1,{micros + 59_999_999L},100,1,0,0,0");
            }
        }
        return output.ToArray();
    }

    [Fact]
    public void Month_has_disjoint_daily_holdouts_and_no_fabricated_edge_when_flat()
    {
        using var archive = new MemoryStream(Month());
        var result = MonthlyWalkForwardResearch.Run("BTCUSDT", 2026, 10, archive);
        Assert.Equal("COMPLETE", result.Status);
        Assert.Equal("INSUFFICIENT_TRADES", result.ResearchGate);
        Assert.Equal(31, result.TestedDays);
        Assert.Single(result.Regimes);
        Assert.Equal(31, result.Regimes[0].Days);
        Assert.Equal("SIDEWAYS", result.Regimes[0].RetrospectiveRegime);
        Assert.Equal(31 * 1440, result.ValidatedCandles);
        Assert.Equal(0, result.CompletedTestTrades);
        Assert.Equal(0m, result.MeanSelectedTestReturnPct);
        Assert.Equal(0, result.PositiveTestDays);
        Assert.False(result.OrderPlacementEnabled);
        Assert.False(result.PaperModified);
        Assert.False(result.PromotionAllowed);
        Assert.All(result.Days, d =>
        {
            Assert.Equal(1, d.ChosenHorizonMinutes);
            Assert.Equal("SIDEWAYS", d.RetrospectiveRegime);
            Assert.Equal(0, d.TestTrades);
            Assert.Equal(0m, d.SelectedTestReturnPct);
        });
    }

    [Fact]
    public void Missing_minute_is_rejected_before_backtesting()
    {
        using var archive = new MemoryStream(Month(gap: true));
        Assert.Throws<InvalidDataException>(() =>
            MonthlyWalkForwardResearch.Run("BTCUSDT", 2026, 10, archive));
    }
}
