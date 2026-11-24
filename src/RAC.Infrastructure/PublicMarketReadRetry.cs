using System.Net;
namespace RAC.Infrastructure;

// Retry only public reads, before any trading state mutation.
public static class PublicMarketReadRetry
{
    public static async Task<T> RunAsync<T>(Func<CancellationToken,Task<T>> read,CancellationToken ct)
    {
        for(var attempt=0;;attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try { return await read(ct); }
            catch(HttpRequestException e) when(attempt<2 && !ct.IsCancellationRequested &&
                (e.StatusCode is null || e.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout))
            { await Task.Delay(TimeSpan.FromMilliseconds(250*(attempt+1)),ct); }
        }
    }
}
