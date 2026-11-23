using System.Net;
using System.Text;
using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

public sealed class PaperExtendedHistoryResearchTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 23, 0, 0, TimeSpan.Zero);
    private static MarketCandlePoint[] Series(int n = 2880, bool flat = false)
    {
        var first = Now.AddMinutes(-n);
        return Enumerable.Range(0, n).Select(i =>
        {
            var t = first.AddMinutes(i);
            var price = flat ? 100m : 100m + i * 0.0005m + (i % 23) * 0.06m;
            return new MarketCandlePoint("BTCUSDT", t,
                t.AddMinutes(1).AddMilliseconds(-1), price,
                price + 0.3m, price - 0.3m, price + (flat ? 0m : 0.02m),
                1m, price, 5);
        }).ToArray();
    }

    [Fact]
    public void Extended_research_requires_support_and_fails_closed_without_trades()
    {
        var result = PaperExtendedHistoryResearch.Run("BTCUSDT", Series(flat: true), Now);
        Assert.Equal("COMPLETE", result.Status);
        Assert.Equal("INSUFFICIENT_TRADES", result.ResearchGate);
        Assert.True(result.FoldCount >= PaperExtendedHistoryResearch.MinimumFolds);
        Assert.Equal(0, result.SelectedTrades);
        Assert.Equal(0, result.PositiveHoldouts);
        Assert.False(result.PaperModified);
        Assert.False(result.OrderPlacementEnabled);
        Assert.False(result.PromotionAllowed);
        Assert.Equal(0m, result.AverageSelectedReturnPct);
        Assert.Equal(result.FoldCount, result.Folds.Count);
        Assert.Equal("INSUFFICIENT_DATA", PaperExtendedHistoryResearch.Run(
            "BTCUSDT", Series(1000), Now).Status);
        Assert.Throws<ArgumentException>(() => PaperExtendedHistoryResearch.Run(
            "INVALID", Series(), Now));
    }

    [Fact]
    public void Extended_historical_folds_are_chronological_and_do_not_overlap()
    {
        var result = PaperExtendedHistoryResearch.Run("BTCUSDT", Series(), Now);
        Assert.Equal("COMPLETE", result.Status);
        Assert.Equal(2880, result.CandleCount);
        Assert.True(result.CoveredUtcDays >= 2);
        Assert.Equal(result.SelectedTrades, result.TotalSelectedWins + result.TotalSelectedLosses);
        Assert.True(result.WorstSelectedDrawdownPct >= 0m);
        Assert.NotEqual("RESEARCH_REVIEW_ONLY", result.ResearchGate);
        for (var i = 0; i < result.Folds.Count; i++)
        {
            var fold = result.Folds[i];
            Assert.True(fold.TrainStartUtc < fold.TestStartUtc);
            Assert.True(fold.TestStartUtc < fold.TestEndUtc);
            Assert.Contains(fold.SelectedHorizonMinutes, PaperTradeHorizonResearch.Horizons);
            if (i > 0) Assert.True(result.Folds[i - 1].TestEndUtc < fold.TestStartUtc);
        }
        var stale = Series().Select(x => x with {
            OpenUtc = x.OpenUtc.AddMinutes(-30), CloseUtc = x.CloseUtc.AddMinutes(-30)
        }).ToArray();
        Assert.Equal("INSUFFICIENT_DATA", PaperExtendedHistoryResearch.Run(
            "BTCUSDT", stale, Now).Status);
    }

    [Fact]
    public async Task Extended_public_history_paginates_three_gets_without_credentials()
    {
        var handler = new PagingHandler();
        using var http = new HttpClient(handler) {
            BaseAddress = new Uri("https://data-api.binance.vision/")
        };
        var client = new BinancePublicMarketClient(http);
        var result = await client.GetExtendedClosedOneMinuteCandlesAsync(
            "BTCUSDT", CancellationToken.None);
        Assert.Equal(2880, result.Count);
        Assert.Equal(3, handler.Requests);
        Assert.False(handler.PrivateRequest);
        for (var i = 1; i < result.Count; i++)
            Assert.Equal(TimeSpan.FromMinutes(1), result[i].OpenUtc - result[i - 1].OpenUtc);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.GetExtendedClosedOneMinuteCandlesAsync("INVALID", CancellationToken.None));
    }

    private sealed class PagingHandler : HttpMessageHandler
    {
        private readonly DateTimeOffset _latest =
            DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60 * 60)
                .AddMinutes(-2);
        public int Requests { get; private set; }
        public bool PrivateRequest { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            Requests++;
            PrivateRequest |= req.Method != HttpMethod.Get ||
                req.RequestUri?.Host != "data-api.binance.vision" ||
                req.Headers.Contains("X-MBX-APIKEY") || req.Headers.Authorization != null;
            var query = req.RequestUri?.Query ?? "";
            Assert.Contains("limit=1000", query);
            var end = _latest;
            var endParam = query.Split('&').FirstOrDefault(x => x.StartsWith("endTime="));
            if (endParam is not null)
            {
                var limit = DateTimeOffset.FromUnixTimeMilliseconds(
                    long.Parse(endParam["endTime=".Length..]));
                end = DateTimeOffset.FromUnixTimeSeconds(limit.ToUnixTimeSeconds() / 60 * 60);
            }
            var lines = new List<string>();
            for (var i = 999; i >= 0; i--)
            {
                var t = end.AddMinutes(-i).ToUnixTimeMilliseconds();
                lines.Add($"[{t},\"100\",\"101\",\"99\",\"100\",\"1\",{t + 59999},\"100\",1]");
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent("[" + string.Join(',', lines) + "]",
                    Encoding.UTF8, "application/json")
            });
        }
    }
}
