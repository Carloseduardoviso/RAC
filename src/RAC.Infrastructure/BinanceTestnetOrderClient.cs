using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAC.Domain;

namespace RAC.Infrastructure;

public sealed record TestnetSymbolFilters(
    decimal MinQty, decimal MaxQty, decimal StepSize,
    decimal MinNotional, decimal MaxNotional,
    decimal MinMarketQty, decimal MaxMarketQty, decimal MarketStepSize,
    bool Trading, bool MarketAllowed);

public static class TestnetOrderValidation
{
    public static TestnetSymbolFilters Parse(JsonElement root, string symbol)
    {
        var symbols = root.GetProperty("symbols");
        var item = symbols.EnumerateArray().SingleOrDefault(x =>
            x.TryGetProperty("symbol",out var code) && code.GetString() == symbol);
        if (item.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Par indisponível na Testnet.");
        decimal Read(JsonElement o,string name) => decimal.Parse(o.GetProperty(name).GetString()!,
            NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture);
        decimal lotMin=0,lotMax=0,lotStep=0,marketMin=0,marketMax=0,marketStep=0,minNotional=0,maxNotional=0;
        foreach (var f in item.GetProperty("filters").EnumerateArray())
        {
            switch(f.GetProperty("filterType").GetString())
            {
                case "LOT_SIZE": lotMin=Read(f,"minQty"); lotMax=Read(f,"maxQty"); lotStep=Read(f,"stepSize"); break;
                case "MARKET_LOT_SIZE": marketMin=Read(f,"minQty");marketMax=Read(f,"maxQty"); marketStep=Read(f,"stepSize");break;
                case "MIN_NOTIONAL": minNotional=Read(f,"minNotional");break;
                case "NOTIONAL":
                    minNotional=Read(f,"minNotional");
                    maxNotional=Read(f,"maxNotional"); break;
            }
        }
        var status = item.GetProperty("status").GetString();
        var market = item.GetProperty("orderTypes").EnumerateArray().Any(x=>x.GetString()=="MARKET");
        if(lotStep<=0 || lotMin<=0 || lotMax<=0 || minNotional<=0)
            throw new InvalidOperationException("Filtros essenciais LOT_SIZE/NOTIONAL indisponíveis; execução bloqueada.");
        return new(lotMin,lotMax,lotStep,minNotional,maxNotional,
            marketMin,marketMax,marketStep,status=="TRADING",market);
    }

    public static decimal ValidateQuantity(decimal requested,decimal tickerUsdt,TestnetSymbolFilters f)
    {
        if (!f.Trading || !f.MarketAllowed || requested<=0 || tickerUsdt<=0)
            throw new InvalidOperationException("Mercado indisponível para ordem de teste.");
        var step = f.MarketStepSize>0 ? f.MarketStepSize : f.StepSize;
        var qty = decimal.Floor(requested/step)*step;
        if(qty<f.MinQty || qty>f.MaxQty || (f.MinMarketQty>0 && qty<f.MinMarketQty) ||
            (f.MaxMarketQty>0 && qty>f.MaxMarketQty))
            throw new InvalidOperationException("Quantidade fora dos filtros da Binance Testnet.");
        var notional = qty*tickerUsdt;
        if(notional<f.MinNotional || (f.MaxNotional>0 && notional>f.MaxNotional))
            throw new InvalidOperationException("Notional fora dos filtros da Binance Testnet.");
        if(qty>requested)throw new InvalidOperationException("Arredondamento aumentou a exposição.");
        return qty;
    }
}

/// <summary>Testnet-only signed transport. Hard-pinned host, never production.</summary>
public sealed class BinanceTestnetOrderClient(HttpClient http, BinanceTestnetCredentialStore store)
{
    private const string Host = "testnet.binance.vision";
    public bool CredentialsConfigured => store.IsConfigured;

    private void CheckEndpoint()
    {
        var u=http.BaseAddress;
        if(u is null || u.Scheme!="https" || !string.Equals(u.Host,Host,StringComparison.Ordinal) ||
            u.Port!=443 || u.AbsolutePath!="/")
            throw new InvalidOperationException("Endpoint diferente da Binance Spot Testnet: bloqueado.");
    }
    public async Task<TestnetSymbolFilters> FiltersAsync(string symbol,CancellationToken ct)
    {
        CheckEndpoint();
        if(!RacMarketSymbols.IsSupported(symbol))throw new ArgumentException("Par não autorizado.");
        using var r=await http.GetAsync("api/v3/exchangeInfo?symbol="+symbol,ct);
        r.EnsureSuccessStatusCode();
        using var json=JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));
        return TestnetOrderValidation.Parse(json.RootElement,symbol);
    }
    public async Task<decimal> TickerAsync(string symbol,CancellationToken ct)
    {
        CheckEndpoint();
        if(!RacMarketSymbols.IsSupported(symbol))throw new ArgumentException("Par não autorizado.");
        using var r=await http.GetAsync("api/v3/ticker/price?symbol="+symbol,ct);
        r.EnsureSuccessStatusCode();
        using var json=JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));
        return decimal.Parse(json.RootElement.GetProperty("price").GetString()!,
            NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture);
    }
    /// <summary>Read available TESTNET balance; never consults production.</summary>
    public async Task<decimal> AvailableAsync(string asset,CancellationToken ct)
    {
        if(string.IsNullOrEmpty(asset) || asset.Length>16 ||
            !asset.All(c=>c>='A'&&c<='Z'))
            throw new ArgumentException("Moeda inválida.");
        using var r=await SignedAsync(HttpMethod.Get,"api/v3/account","omitZeroBalances=false",ct);
        r.EnsureSuccessStatusCode();
        using var json=JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct));
        foreach(var row in json.RootElement.GetProperty("balances").EnumerateArray())
        {
            if(row.GetProperty("asset").GetString()!=asset)continue;
            return decimal.Parse(row.GetProperty("free").GetString()!,
                NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture);
        }
        return 0m;
    }
    public async Task<TestnetExchangeOrder?> QueryAsync(string symbol,string clientId,CancellationToken ct)
    {
        if(!RacMarketSymbols.IsSupported(symbol) || !ValidId(clientId))
            throw new ArgumentException("Identificador de ordem Testnet inválido.");
        using var r=await SignedAsync(HttpMethod.Get,"api/v3/order",
            "symbol="+symbol+"&origClientOrderId="+Uri.EscapeDataString(clientId),ct);
        if(r.StatusCode==System.Net.HttpStatusCode.BadRequest ||
            r.StatusCode==System.Net.HttpStatusCode.NotFound)
        {
            // -2013 is specifically "Order does not exist"; unknown errors
            // must NOT cause another POST.
            var body=await r.Content.ReadAsStringAsync(ct);
            using var parsed=JsonDocument.Parse(body);
            if(parsed.RootElement.TryGetProperty("code",out var code)&&code.GetInt32()==-2013)
                return null;
        }
        r.EnsureSuccessStatusCode();
        return ParseOrder(await r.Content.ReadAsStringAsync(ct));
    }
    public async Task<TestnetExchangeOrder> MarketAsync(string symbol,string side,
        decimal quantity,string clientId,CancellationToken ct)
    {
        if(!RacMarketSymbols.IsSupported(symbol) || side is not ("BUY" or "SELL") ||
            !ValidId(clientId) || quantity<=0)throw new ArgumentException("Ordem Testnet inválida.");
        var qty=quantity.ToString("0.################",CultureInfo.InvariantCulture);
        using var r=await SignedAsync(HttpMethod.Post,"api/v3/order",
            "symbol="+symbol+"&side="+side+"&type=MARKET&quantity="+qty+
            "&newClientOrderId="+Uri.EscapeDataString(clientId)+"&newOrderRespType=RESULT",ct);
        r.EnsureSuccessStatusCode();
        return ParseOrder(await r.Content.ReadAsStringAsync(ct));
    }
    public async Task<TestnetExchangeOrder> CancelAsync(string symbol,string clientId,CancellationToken ct)
    {
        if(!RacMarketSymbols.IsSupported(symbol)||!ValidId(clientId))
            throw new ArgumentException("Ordem Testnet inválida.");
        using var r=await SignedAsync(HttpMethod.Delete,"api/v3/order",
            "symbol="+symbol+"&origClientOrderId="+Uri.EscapeDataString(clientId),ct);
        r.EnsureSuccessStatusCode();
        return ParseOrder(await r.Content.ReadAsStringAsync(ct));
    }
    private async Task<HttpResponseMessage> SignedAsync(HttpMethod method,string endpoint,
        string parameters,CancellationToken ct)
    {
        CheckEndpoint();
        var (key,secret)=store.Load();
        using var time=await http.GetAsync("api/v3/time",ct);
        time.EnsureSuccessStatusCode();
        using var timestamp=JsonDocument.Parse(await time.Content.ReadAsStringAsync(ct));
        var ts=timestamp.RootElement.GetProperty("serverTime").GetInt64();
        var query=parameters+"&recvWindow=5000&timestamp="+ts;
        var bytes=Encoding.UTF8.GetBytes(secret);
        try
        {
            var sig=Convert.ToHexString(HMACSHA256.HashData(bytes,Encoding.UTF8.GetBytes(query))).ToLowerInvariant();
            using var request=new HttpRequestMessage(method,endpoint+"?"+query+"&signature="+sig);
            request.Headers.TryAddWithoutValidation("X-MBX-APIKEY",key);
            // The signed request is never followed by redirects.
            return await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct);
        }
        finally {CryptographicOperations.ZeroMemory(bytes);}
    }
    private static bool ValidId(string value) =>
        value.Length is >= 6 and <= 36 && value.All(c=>char.IsAsciiLetterOrDigit(c)||c=='-'||c=='_');
    private static TestnetExchangeOrder ParseOrder(string payload)
    {
        using var json=JsonDocument.Parse(payload);
        var root=json.RootElement;
        decimal D(string name) => root.TryGetProperty(name,out var v) ?
            decimal.Parse(v.GetString()!,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture):0m;
        return new TestnetExchangeOrder(
            root.GetProperty("clientOrderId").GetString()!,
            root.GetProperty("symbol").GetString()!,
            root.GetProperty("status").GetString()!,
            root.GetProperty("orderId").ToString(),
            D("executedQty"),D("cummulativeQuoteQty"));
    }
}
public sealed record TestnetExchangeOrder(string ClientOrderId,string Symbol,string Status,
    string OrderId,decimal ExecutedQuantity,decimal QuoteQuantity);
