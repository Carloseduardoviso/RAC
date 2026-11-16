using System.Net;
using System.Text;
using System.Text.Json;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

public sealed class BinancePublicMarketTests
{
    [Fact]
    public void Quote_parser_accepts_supported_symbols_and_invariant_decimals()
    {
        using var json = JsonDocument.Parse(
            """[{"symbol":"BTCUSDT","price":"65001.12500000"},{"symbol":"DOGEUSDT","price":"0.125"},{"symbol":"OTHERUSDT","price":"2.0"}]""");
        var quotes = BinancePublicMarketClient.ParsePrices(json.RootElement);
        Assert.Equal(2, quotes.Count);
        Assert.Equal(65001.125m, quotes.Single(x => x.Symbol == "BTCUSDT").PriceUsdt);
        Assert.Equal(0.125m, quotes.Single(x => x.Symbol == "DOGEUSDT").PriceUsdt);
        Assert.DoesNotContain(quotes, x => x.Symbol == "OTHERUSDT");
    }

    [Fact]
    public void Invalid_or_duplicate_quotes_are_rejected()
    {
        using var duplicates = JsonDocument.Parse(
            """[{"symbol":"BTCUSDT","price":"1"},{"symbol":"BTCUSDT","price":"2"}]""");
        using var zero = JsonDocument.Parse("""[{"symbol":"BTCUSDT","price":"0"}]""");
        Assert.Throws<FormatException>(() => BinancePublicMarketClient.ParsePrices(duplicates.RootElement));
        Assert.Throws<FormatException>(() => BinancePublicMarketClient.ParsePrices(zero.RootElement));
    }

    [Fact]
    public void Candle_parser_only_keeps_closed_bars_and_validates_ohlcv()
    {
        const long openTime = 1_735_689_600_000; // Historical UTC, well before the test.
        var finished = $"[{openTime},\"10.00\",\"12.00\",\"9.00\",\"11.00\",\"5.00\",{openTime + 59999},\"55.00\",10,\"0\",\"0\",\"0\"]";
        var now = DateTimeOffset.UtcNow;
        var forming = $"[{now.ToUnixTimeMilliseconds()},\"10.00\",\"12.00\",\"9.00\",\"11.00\",\"5.00\",{now.ToUnixTimeMilliseconds() + 59999},\"55.00\",10,\"0\",\"0\",\"0\"]";
        using var json = JsonDocument.Parse("[" + finished + "," + forming + "]");
        var result = BinancePublicMarketClient.ParseClosedCandles("BTCUSDT", json.RootElement, now);
        var candle = Assert.Single(result);
        Assert.Equal(11m, candle.Close);
        Assert.Equal(5m, candle.Volume);
        Assert.Equal(10, candle.Trades);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BinancePublicMarketClient.ParseClosedCandles("INVALID", json.RootElement, now));
    }

    [Fact]
    public async Task Market_client_uses_only_public_get_without_credentials()
    {
        var handler = new FakeHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://data-api.binance.vision/") };
        var client = new BinancePublicMarketClient(http);
        var result = await client.GetPricesAsync(CancellationToken.None);
        Assert.Single(result);
        Assert.Equal("BTCUSDT", result[0].Symbol);
        Assert.Equal(HttpMethod.Get, handler.RequestMethod);
        Assert.Equal("data-api.binance.vision", handler.RequestHost);
        Assert.True(handler.QueryHadSymbols);
        Assert.False(handler.HadApiKey);
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        public HttpMethod? RequestMethod { get; private set; }
        public string? RequestHost { get; private set; }
        public bool QueryHadSymbols { get; private set; }
        public bool HadApiKey { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            RequestMethod = request.Method;
            RequestHost = request.RequestUri?.Host;
            QueryHadSymbols = request.RequestUri?.Query.Contains("symbols=", StringComparison.Ordinal) ?? false;
            HadApiKey = request.Headers.Contains("X-MBX-APIKEY");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""[{"symbol":"BTCUSDT","price":"65000.01"}]""",
                    Encoding.UTF8, "application/json")
            });
        }
    }
}
