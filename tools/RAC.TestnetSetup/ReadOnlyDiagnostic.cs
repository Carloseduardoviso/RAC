using System.Net;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.TestnetSetup;

/// <summary>
/// Opt-in read-only diagnostic. Only GET /api/v3/account, /api/v3/time,
/// /api/v3/exchangeInfo, /api/v3/ticker/price are allowed.
/// No trading/cancel methods or production endpoints are used.
/// </summary>
public static class ReadOnlyDiagnostic
{
    public static async Task<int> RunAsync(BinanceTestnetCredentialStore store)
    {
        Console.WriteLine("RAC Testnet: diagnóstico EXCLUSIVAMENTE LEITURA, sem envio de ordens.");
        if(!store.IsConfigured)
        {
            Console.WriteLine("ERRO: não há credenciais TESTNET armazenadas.");
            return 2;
        }

        using var http=new HttpClient(new ReadOnlyHandler(new HttpClientHandler
        {
            AllowAutoRedirect=false,
            UseCookies=false
        }))
        {
            BaseAddress=new Uri("https://testnet.binance.vision/"),
            Timeout=TimeSpan.FromSeconds(12)
        };
        var client=new BinanceTestnetOrderClient(http,store);
        var failures=0;
        Console.WriteLine("ENDPOINT: https://testnet.binance.vision (somente GET)");
        Console.WriteLine("=== AUTENTICAÇÃO E SALDOS FICTÍCIOS ===");
        foreach(var asset in new[]{"USDT","BTC"})
        {
            try
            {
                var amount=await client.AvailableAsync(asset,CancellationToken.None);
                Console.WriteLine($"{asset}: saldo livre Testnet = {amount:0.########}");
            }
            catch(HttpRequestException e)
            {
                failures++;
                Console.WriteLine($"{asset}: falha HTTP {(int?)e.StatusCode ?? 0} (sem dados sensíveis).");
            }
            catch(Exception e)
            {
                failures++;
                Console.WriteLine($"{asset}: falha {e.GetType().Name} (sem dados sensíveis).");
            }
        }

        Console.WriteLine("=== FILTROS DOS 12 PARES DA TESTNET ===");
        var usable=0;
        foreach(var symbol in RacMarketSymbols.Supported)
        {
            try
            {
                var f=await client.FiltersAsync(symbol,CancellationToken.None);
                var ready=f.Trading && f.MarketAllowed;
                Console.WriteLine($"{symbol}: {(ready?"TRADING/MARKET":"INDISPONÍVEL")}; "+
                    $"minQty={f.MinQty:0.########}; step={f.StepSize:0.########}; "+
                    $"minNotional={f.MinNotional:0.########} USDT");
                if(ready)usable++;
                else failures++;
            }
            catch(HttpRequestException e)
            {
                failures++;
                Console.WriteLine($"{symbol}: erro HTTP {(int?)e.StatusCode ?? 0} (sem dados sensíveis).");
            }
            catch(Exception e)
            {
                failures++;
                Console.WriteLine($"{symbol}: erro {e.GetType().Name} (sem dados sensíveis).");
            }
        }
        Console.WriteLine($"RESUMO: pares disponíveis {usable}/12; problemas {failures}; ordens enviadas 0.");
        return failures==0?0:3;
    }

    private sealed class ReadOnlyHandler(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        private static readonly HashSet<string> ApprovedPaths = new(StringComparer.Ordinal)
        {"/api/v3/time","/api/v3/account","/api/v3/exchangeInfo","/api/v3/ticker/price"};
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken ct)
        {
            var u=request.RequestUri;
            if(request.Method!=HttpMethod.Get || u is null || u.Scheme!="https" ||
                u.Host!="testnet.binance.vision" || u.Port!=443 ||
                !ApprovedPaths.Contains(u.AbsolutePath))
                throw new InvalidOperationException("Diagnóstico read-only bloqueou solicitação de rede.");
            return base.SendAsync(request,ct);
        }
    }
}
