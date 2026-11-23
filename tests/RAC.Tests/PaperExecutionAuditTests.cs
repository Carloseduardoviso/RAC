using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class PaperExecutionAuditTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Cash_conservation_and_round_trip_cost_are_reconciled(int multiplier)
    {
        var k = (decimal)multiplier;
        var buy = PaperExecutionAudit.BuyAll(100m, 100m, k);
        var sell = PaperExecutionAudit.SellAll(buy.FilledQuantity, 100m, k);
        var feeRate = PaperProfitabilityDiagnostics.FeePctPerSide / 100m * k;
        var impactRate = (PaperProfitabilityDiagnostics.SlippagePctPerSide +
            PaperProfitabilityDiagnostics.HalfSpreadPctPerSide) / 100m * k;
        Assert.Equal(100m, buy.FeePaidInCash +
            buy.FilledQuantity * buy.ImpactAdjustedEntryQuote);
        Assert.Equal(100m * (1m + impactRate), buy.ImpactAdjustedEntryQuote);
        Assert.Equal(100m * feeRate, buy.FeePaidInCash);
        Assert.Equal(100m * (1m - impactRate), sell.ImpactAdjustedExitQuote);
        Assert.Equal(sell.Quantity * sell.ImpactAdjustedExitQuote,
            sell.FeePaidInCash + sell.NetCashAfter);
        Assert.True(sell.NetCashAfter < buy.CashBefore);
        var expected = 100m * (1m - feeRate) / (1m + impactRate) *
            (1m - impactRate) * (1m - feeRate);
        Assert.InRange(Math.Abs(expected - sell.NetCashAfter), 0m, 0.0000000000000000001m);
    }

    [Fact]
    public void Doubling_costs_reduces_equal_quote_round_trip_proceeds()
    {
        var normal = PaperExecutionAudit.BuyAll(100m, 100m, 1m);
        var stressed = PaperExecutionAudit.BuyAll(100m, 100m, 2m);
        var normalExit = PaperExecutionAudit.SellAll(normal.FilledQuantity, 100m, 1m);
        var stressedExit = PaperExecutionAudit.SellAll(stressed.FilledQuantity, 100m, 2m);
        Assert.True(stressedExit.NetCashAfter < normalExit.NetCashAfter);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaperExecutionAudit.BuyAll(100m, 100m, 3m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaperExecutionAudit.SellAll(1m, 100m, 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaperExecutionAudit.BuyAll(0m, 100m, 1m));
    }

    [Fact]
    public void Intrabar_stop_uses_stop_quote_and_gaps_never_fill_above_open()
    {
        Assert.Equal(99m, PaperExecutionAudit.ConservativeStopFill(99m, 100m, 98m));
        Assert.Equal(95m, PaperExecutionAudit.ConservativeStopFill(99m, 95m, 94m));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaperExecutionAudit.ConservativeStopFill(99m, 100m, 100m));
    }

    [Fact]
    public void Simulator_rejects_impossible_ohlc_even_when_no_trade_is_chosen()
    {
        var start = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var candles = Enumerable.Range(0, 100).Select(i =>
            new MarketCandlePoint("BTCUSDT", start.AddMinutes(i),
                start.AddMinutes(i + 1).AddMilliseconds(-1),
                100m, 101m, 99m, 100m, 1m, 100m, 5)).ToArray();
        Assert.Equal("COMPLETE", PaperTradeHorizonResearch.SimulateSlice(
            "BTCUSDT", candles, 15, 1m, "NO_TRADE").Status);
        var invalid = candles.ToArray();
        invalid[42] = invalid[42] with { Close = 102m };
        Assert.Equal("BLOCKED", PaperTradeHorizonResearch.SimulateSlice(
            "BTCUSDT", invalid, 15, 1m, "NO_TRADE").Status);
        invalid = candles.ToArray();
        invalid[42] = invalid[42] with { Open = 98m };
        Assert.Equal("BLOCKED", PaperTradeHorizonResearch.SimulateSlice(
            "BTCUSDT", invalid, 15).Status);
        invalid = candles.ToArray();
        invalid[42] = invalid[42] with {
            CloseUtc = invalid[42].OpenUtc.AddMinutes(1)
        };
        Assert.Equal("BLOCKED", PaperTradeHorizonResearch.SimulateSlice(
            "BTCUSDT", invalid, 15).Status);
        invalid = candles.ToArray();
        invalid[42] = invalid[42] with { Volume = -1m };
        Assert.Equal("BLOCKED", PaperTradeHorizonResearch.SimulateSlice(
            "BTCUSDT", invalid, 15).Status);
        invalid = candles.ToArray();
        invalid[42] = invalid[42] with { CloseUtc = invalid[42].OpenUtc.AddMinutes(2) };
        Assert.Equal("BLOCKED", PaperTradeHorizonResearch.SimulateSlice(
            "BTCUSDT", invalid, 15).Status);
    }
}
