namespace RAC.Application;

// Discovery catalog only. Inclusion does not authorize scraping or trading.
public static class FinancialSourceCatalog
{
    public sealed record Source(string Id,string Name,string HomePage,string Category);
    public static IReadOnlyList<Source> Sources { get; } = Array.AsReadOnly(new Source[]
    {
        new("binance","Binance","https://www.binance.com/","EXCHANGE_OFFICIAL"),
        new("google-news","Google News","https://news.google.com/","AGGREGATOR"),
        new("b3","B3","https://www.b3.com.br/","EXCHANGE_OFFICIAL"),
        new("bcb","Banco Central","https://www.bcb.gov.br/","CENTRAL_BANK"),
        new("fed","Federal Reserve","https://www.federalreserve.gov/","CENTRAL_BANK"),
        new("reuters","Reuters","https://www.reuters.com/","JOURNALISM"),
        new("bloomberg","Bloomberg","https://www.bloomberg.com/","JOURNALISM"),
        new("investing","Investing.com","https://www.investing.com/","MARKET_DATA"),
        new("coindesk","CoinDesk","https://www.coindesk.com/","JOURNALISM"),
        new("coinmarketcap","CoinMarketCap","https://coinmarketcap.com/","MARKET_DATA"),
        new("coingecko","CoinGecko","https://www.coingecko.com/","MARKET_DATA"),
        new("infomoney","InfoMoney","https://www.infomoney.com.br/","JOURNALISM"),
        new("tradingview","TradingView","https://www.tradingview.com/","ANALYSIS"),
        new("berkshire","Berkshire Hathaway","https://www.berkshirehathaway.com/","INVESTOR_RESEARCH"),
        new("bridgewater","Bridgewater","https://www.bridgewater.com/","INVESTOR_RESEARCH")
    });
}
