using RAC.Application;
namespace RAC.Infrastructure;
public sealed class SamuelBinanceDepthClient(HttpClient http)
{
    private readonly SemaphoreSlim gate=new(1,1);
    private DateTimeOffset nextAllowed=DateTimeOffset.MinValue;
    public async Task<SamuelDepthSnapshot> ReadAsync(string symbol,CancellationToken ct)
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(symbol,@"\A[A-Z0-9]{2,16}USDT\z"))
            throw new ArgumentException("Invalid symbol.");
        await gate.WaitAsync(ct);
        try
        {
            var start=DateTimeOffset.UtcNow;
            if(start<nextAllowed) throw new InvalidOperationException("Depth cooldown.");
            nextAllowed=start.AddSeconds(2);
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var response=await http.GetAsync("https://api.binance.com/api/v3/depth?symbol="+symbol+"&limit=100",timeout.Token);
            if((int)response.StatusCode is 429 or 418)
            {
                var retry=response.Headers.RetryAfter;
                var until=retry?.Date ?? DateTimeOffset.UtcNow.Add(retry?.Delta ?? TimeSpan.FromMinutes(5));
                if(until>nextAllowed) nextAllowed=until;
            }
            response.EnsureSuccessStatusCode();
            var json=await response.Content.ReadAsStringAsync(timeout.Token);
            return SamuelDepthParser.Parse(json,start,DateTimeOffset.UtcNow);
        }
        finally { gate.Release(); }
    }
}
