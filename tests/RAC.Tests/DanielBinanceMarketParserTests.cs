using RAC.Infrastructure;
using RAC.Application;
namespace RAC.Tests;

public sealed class DanielBinanceMarketParserTests
{
    [Fact]
    public void JoinsOnlyActiveSpotUsdtMarketsAndPreservesTimestamp()
    {
        const string info = """
        {"symbols":[{"symbol":"ABCUSDT","status":"TRADING","quoteAsset":"USDT","isSpotTradingAllowed":true},{"symbol":"XYZUSDT","status":"BREAK","quoteAsset":"USDT","isSpotTradingAllowed":true}]}
        """;
        const string tickers = """
        [{"symbol":"ABCUSDT","lowPrice":"100","highPrice":"103","quoteVolume":"2000000","bidPrice":"101","askPrice":"101.1","closeTime":1800000000000},{"symbol":"XYZUSDT"}]
        """;
        var now=DateTimeOffset.FromUnixTimeMilliseconds(1800000000000);
        var row=Assert.Single(DanielBinanceMarketParser.Parse(info,tickers,now));
        Assert.Equal("ABCUSDT",row.Symbol);
        Assert.Equal(3m,row.RangePercent);
        Assert.Equal(now,row.ObservedUtc);
        Assert.True(DanielMarketDiscovery.Evaluate(row,now).ResearchCandidate);
        Assert.Empty(DanielBinanceMarketParser.Parse(info,tickers,now.AddMinutes(3)));
    }
    [Fact]
    public void CoverageDistinguishesMissingAndRejectedMarkets()
    {
        const string info="""
        {"symbols":[{"symbol":"AAAUSDT","status":"TRADING","quoteAsset":"USDT","isSpotTradingAllowed":true},{"symbol":"BBBUSDT","status":"TRADING","quoteAsset":"USDT","isSpotTradingAllowed":true},{"symbol":"CCCUSDT","status":"TRADING","quoteAsset":"USDT","isSpotTradingAllowed":true}]}
        """;
        const string tickers="""
        [{"symbol":"AAAUSDT","lowPrice":"100","highPrice":"103","quoteVolume":"2000000","bidPrice":"101","askPrice":"101.1","closeTime":1800000000000},{"symbol":"BBBUSDT","lowPrice":"0","highPrice":"103"}]
        """;
        var coverage=DanielBinanceMarketParser.ParseCoverage(info,tickers,DateTimeOffset.FromUnixTimeMilliseconds(1800000000000));
        Assert.Equal(3,coverage.ExpectedMarkets);
        Assert.Equal("AAAUSDT",Assert.Single(coverage.Observations).Symbol);
        Assert.Equal("BBBUSDT",Assert.Single(coverage.RejectedTickers));
        Assert.Equal("CCCUSDT",Assert.Single(coverage.MissingTickers));
        Assert.False(coverage.TradingAuthorized);
    }
}
