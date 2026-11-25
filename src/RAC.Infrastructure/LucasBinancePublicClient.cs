using RAC.Application;
using System.Text.Json;
namespace RAC.Infrastructure;
public sealed class LucasBinancePublicClient(HttpClient http)
{
    public async Task<LucasExchangeObservation> ReadAsync(CancellationToken ct)
    {
        var start=DateTimeOffset.UtcNow;
        int? status=null;
        DateTimeOffset? server=null;
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            using var response=await http.GetAsync("https://api.binance.com/api/v3/time",timeout.Token);
            status=(int)response.StatusCode;
            if(response.IsSuccessStatusCode)
            {
                var body=await response.Content.ReadAsStringAsync(timeout.Token);
                if(body.Length<=4096)
                {
                    using var json=JsonDocument.Parse(body);
                    server=DateTimeOffset.FromUnixTimeMilliseconds(json.RootElement.GetProperty("serverTime").GetInt64());
                }
            }
        }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested){}
        catch(HttpRequestException){}
        catch(JsonException){}
        catch(KeyNotFoundException){}
        catch(InvalidOperationException){}
        catch(ArgumentOutOfRangeException){}
        ct.ThrowIfCancellationRequested();
        return new("BINANCE","PUBLIC",start,DateTimeOffset.UtcNow,server,status);
    }
}
