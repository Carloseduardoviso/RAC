using System.IO.Compression;
using System.Text;
using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class RacFixedHoldoutReplayTests
{
    private static MemoryStream MonthFixture()
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("BTCUSDT-1m-2026-10.csv",
                CompressionLevel.Fastest);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            var beginning = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
            for (var minute = 0; minute < 31 * 1440; minute++)
            {
                var time = beginning.AddMinutes(minute).ToUnixTimeMilliseconds() * 1000L;
                writer.WriteLine($"{time},100,102,98,101,1,{time + 59_999_999L},101,5,1,101,0");
            }
        }
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void Frozen_no_trade_replays_as_abstention_not_active_profit()
    {
        using var archive = MonthFixture();
        var result = RacFixedHoldoutReplay.Evaluate("BTCUSDT", 2026, 10,
            archive, "NO_TRADE", 1);
        Assert.Equal(31, result.TestedDays);
        Assert.Equal(0, result.ClosedTrades);
        Assert.Equal(0m, result.MeanNetReturnPct);
        Assert.Equal("NO_TRADE_BASELINE_NOT_ACTIVE_REPLAY", result.EvaluationStatus);
        Assert.False(result.ReviewEligible);
        Assert.False(result.PaperModified);
        Assert.False(result.OrderPlacementEnabled);
        Assert.False(result.PromotionAllowed);
    }

    [Theory]
    [InlineData("NO_TRADE", 15)]
    [InlineData("BASELINE", 1)]
    [InlineData("UNSUPPORTED", 30)]
    public void Invalid_frozen_settings_are_rejected_before_replay(
        string gate, int horizon)
    {
        using var archive = new MemoryStream();
        Assert.Throws<InvalidDataException>(() =>
            RacFixedHoldoutReplay.Evaluate("BTCUSDT", 2026, 10,
                archive, gate, horizon));
    }

    private static MarketCandlePoint[] Window(int count = 720)
    {
        var start = new DateTimeOffset(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);
        return Enumerable.Range(0, count).Select(i =>
        {
            var time = start.AddMinutes(i);
            var open = 100m + i * 0.002m + (i % 19) * 0.08m;
            return new MarketCandlePoint("BTCUSDT", time,
                time.AddMinutes(1).AddMilliseconds(-1),
                open, open + 0.25m, open - 0.25m,
                open + 0.02m, 1m, open, 5);
        }).ToArray();
    }

    [Fact]
    public void Active_fixed_window_is_deterministic_at_both_cost_levels()
    {
        var bars = Window();
        var normal = RacFixedHoldoutReplay.AssessWindow(
            "BTCUSDT", bars, "BASELINE", 15, 1m);
        var stressed = RacFixedHoldoutReplay.AssessWindow(
            "BTCUSDT", bars, "BASELINE", 15, 2m);
        Assert.Equal(normal, RacFixedHoldoutReplay.AssessWindow(
            "BTCUSDT", bars, "BASELINE", 15, 1m));
        Assert.Equal(stressed, RacFixedHoldoutReplay.AssessWindow(
            "BTCUSDT", bars, "BASELINE", 15, 2m));
        Assert.True(normal.ClosedTrades >= 0);
        Assert.True(stressed.ClosedTrades >= 0);
        Assert.True(normal.MaxDrawdownPct >= 0m);
        Assert.True(stressed.MaxDrawdownPct >= 0m);
    }

    [Fact]
    public void Active_window_refuses_invalid_costs_and_incomplete_data()
    {
        var bars = Window();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RacFixedHoldoutReplay.AssessWindow(
                "BTCUSDT", bars, "BASELINE", 15, 3m));
        Assert.Throws<InvalidDataException>(() =>
            RacFixedHoldoutReplay.AssessWindow(
                "BTCUSDT", Window(719), "BASELINE", 15, 1m));
        Assert.Throws<InvalidDataException>(() =>
            RacFixedHoldoutReplay.AssessWindow(
                "BTCUSDT", bars, "NO_TRADE", 15, 1m));
    }

    [Fact]
    public void Archive_identity_is_checked_before_no_trade_baseline()
    {
        using var archive = MonthFixture();
        Assert.Throws<InvalidDataException>(() =>
            RacFixedHoldoutReplay.Evaluate("BTCUSDT", 2026, 9,
                archive, "NO_TRADE", 1));
    }
}
