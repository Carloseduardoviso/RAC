namespace RAC.Infrastructure;

public static class GabrielResearchLease
{
    public static async Task<T> RunAsync<T>(Func<CancellationToken,Task<T>> work,
        Func<CancellationToken,Task<bool>> renew,TimeSpan interval,CancellationToken ct)
    {
        if(interval<=TimeSpan.Zero || interval>=TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(interval));
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct);
        async Task Heartbeat()
        {
            using var timer=new PeriodicTimer(interval);
            try
            {
                while(await timer.WaitForNextTickAsync(linked.Token))
                    if(!await renew(linked.Token))
                        throw new InvalidOperationException("Research lease lost.");
            }
            catch(OperationCanceledException) when(linked.IsCancellationRequested) {}
            catch { linked.Cancel(); throw; }
        }
        var heartbeat=Heartbeat();
        try
        {
            var result=await work(linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            return result;
        }
        finally
        {
            linked.Cancel();
            await heartbeat;
        }
    }
}
