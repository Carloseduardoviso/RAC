using RAC.Application;
namespace RAC.Infrastructure;

public sealed class DanielBinancePublicClient(HttpClient http)
{
    private readonly SemaphoreSlim gate = new(1,1);
    private DateTimeOffset nextAllowed = DateTimeOffset.MinValue;

    public async Task<IReadOnlyList<DanielMarketObservation>> ReadAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            if(DateTimeOffset.UtcNow<nextAllowed) throw new InvalidOperationException("Market discovery cooldown.");
            nextAllowed=DateTimeOffset.UtcNow.AddMinutes(1);
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));
            var info=await GetAsync("exchangeInfo",timeout.Token);
            var tickers=await GetAsync("ticker/24hr",timeout.Token);
            return DanielBinanceMarketParser.Parse(info,tickers,DateTimeOffset.UtcNow);
        }
        finally { gate.Release(); }
    }

    private async Task<string> GetAsync(string path,CancellationToken ct)
    {
        using var response=await http.GetAsync("https://api.binance.com/api/v3/"+path,ct);
        if((int)response.StatusCode is 429 or 418)
        {
            var retry=response.Headers.RetryAfter;
            var until=retry?.Date ?? DateTimeOffset.UtcNow.Add(retry?.Delta ?? TimeSpan.FromMinutes(5));
            if(until>nextAllowed) nextAllowed=until;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
