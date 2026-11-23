using System.IO.Compression;
using System.Text;
using RAC.Application;

namespace RAC.Tests;

public sealed class MonthlyArchiveResearchTests
{
    private static byte[] Fixture(bool withGap = false)
    {
        using var data = new MemoryStream();
        using (var zip = new ZipArchive(data, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("BTCUSDT-1m-2026-10.csv", CompressionLevel.Fastest);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8, leaveOpen: false);
            var month = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
            const int count = 31 * 1440;
            for (var i = 0; i < count; i++)
            {
                var time = month.AddMinutes(i + (withGap && i >= 1000 ? 1 : 0))
                    .ToUnixTimeMilliseconds() * 1000L;
                // BTCUSDT after 2025: Binance monthly archive microseconds.
                writer.WriteLine($"{time},100,102,98,101,1,{time + 59_999_999L},101,5,1,101,0");
            }
        }
        return data.ToArray();
    }

    [Fact]
    public void Complete_month_is_validated_and_daily_regimes_are_descriptive_only()
    {
        var bytes = Fixture();
        using var stream = new MemoryStream(bytes);
        var report = MonthlyArchiveResearch.Analyze("BTCUSDT", 2026, 10, stream);
        Assert.Equal(31 * 1440, report.ValidatedCandles);
        Assert.Equal(31, report.CalendarDays);
        Assert.Equal(31, report.SidewaysDays);
        Assert.Equal(0, report.UpDays + report.DownDays);
        Assert.False(report.PaperModified);
        Assert.False(report.OrderPlacementEnabled);
        Assert.False(report.PromotionAllowed);
        Assert.All(report.Days, day => Assert.Equal(1440, day.Candles));
        Assert.Equal(new DateTimeOffset(2026, 10, 31, 23, 59, 0, TimeSpan.Zero),
            report.LastOpenUtc);
    }

    [Fact]
    public void Gaps_or_inconsistent_archive_identity_fail_closed()
    {
        using var gap = new MemoryStream(Fixture(withGap: true));
        Assert.Throws<InvalidDataException>(() =>
            MonthlyArchiveResearch.Analyze("BTCUSDT", 2026, 10, gap));
        using var valid = new MemoryStream(Fixture());
        Assert.Throws<InvalidDataException>(() =>
            MonthlyArchiveResearch.Analyze("BTCUSDT", 2026, 9, valid));
        using var supported = new MemoryStream(Fixture());
        Assert.Throws<ArgumentException>(() =>
            MonthlyArchiveResearch.Analyze("BADPAIR", 2026, 10, supported));
    }
}
