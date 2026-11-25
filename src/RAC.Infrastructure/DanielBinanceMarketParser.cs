using System.Text.Json;
using System.Globalization;
using RAC.Application;

namespace RAC.Infrastructure;

public sealed record DanielMarketCoverage(IReadOnlyList<DanielMarketObservation> Observations,IReadOnlyList<string> MissingTickers,IReadOnlyList<string> RejectedTickers,int ExpectedMarkets)
{ public bool TradingAuthorized => false; }

public static class DanielBinanceMarketParser
{
    private static decimal Number(JsonElement e,string key) =>
        decimal.Parse(e.GetProperty(key).GetString()!,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture);

    public static IReadOnlyList<DanielMarketObservation> Parse(string exchangeInfo,string tickers,DateTimeOffset receivedUtc)
    {
        return ParseCoverage(exchangeInfo,tickers,receivedUtc).Observations;
    }
    public static DanielMarketCoverage ParseCoverage(string exchangeInfo,string tickers,DateTimeOffset receivedUtc)
    {
        using var info=JsonDocument.Parse(exchangeInfo);
        using var prices=JsonDocument.Parse(tickers);
        var allowed=info.RootElement.GetProperty("symbols").EnumerateArray()
            .Where(x=>x.GetProperty("status").GetString()=="TRADING" &&
                x.GetProperty("quoteAsset").GetString()=="USDT" &&
                x.GetProperty("isSpotTradingAllowed").GetBoolean())
            .Select(x=>x.GetProperty("symbol").GetString()!).ToHashSet(StringComparer.Ordinal);
        var result=new List<DanielMarketObservation>();
        var seen=new HashSet<string>(StringComparer.Ordinal);
        foreach(var ticker in prices.RootElement.EnumerateArray())
        {
            var symbol=ticker.GetProperty("symbol").GetString()!;
            if(!allowed.Contains(symbol)) continue;
            if(!seen.Add(symbol)) throw new FormatException("Duplicate market ticker.");
            var low=Number(ticker,"lowPrice");
            var high=Number(ticker,"highPrice");
            if(low<=0 || high<low) continue;
            var observed=DateTimeOffset.FromUnixTimeMilliseconds(ticker.GetProperty("closeTime").GetInt64());
            if(observed>receivedUtc || receivedUtc-observed>TimeSpan.FromMinutes(2)) continue;
            result.Add(new(symbol,true,Number(ticker,"quoteVolume"),Number(ticker,"bidPrice"),
                Number(ticker,"askPrice"),(high-low)/low*100m,observed));
        }
        var accepted=result.Select(x=>x.Symbol).ToHashSet(StringComparer.Ordinal);
        return new(result.AsReadOnly(),Array.AsReadOnly(allowed.Except(seen).OrderBy(x=>x,StringComparer.Ordinal).ToArray()),Array.AsReadOnly(seen.Except(accepted).OrderBy(x=>x,StringComparer.Ordinal).ToArray()),allowed.Count);
    }
}
