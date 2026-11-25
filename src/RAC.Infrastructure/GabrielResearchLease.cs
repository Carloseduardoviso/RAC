namespace RAC.Infrastructure;

public static class GabrielResearchLease
{
    public static async Task<T> RunAsync<T>(Func<CancellationToken,Task<T>> work,
        Func<CancellationToken,Task<bool>> renew,TimeSpan interval,CancellationToken ct,TimeSpan? renewalTimeout=null)
    {
        if(interval<=TimeSpan.Zero || interval>=TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(interval));
        var timeout=renewalTimeout??TimeSpan.FromSeconds(30);
        if(timeout<=TimeSpan.Zero || timeout>=TimeSpan.FromMinutes(5)-interval)
            throw new ArgumentOutOfRangeException(nameof(renewalTimeout));
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct);
        async Task Heartbeat()
        {
            using var timer=new PeriodicTimer(interval);
            try
            {
                while(await timer.WaitForNextTickAsync(linked.Token))
                {
                    using var renewal=CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
                    renewal.CancelAfter(timeout);
                    if(!await renew(renewal.Token).WaitAsync(renewal.Token))
                        throw new InvalidOperationException("Research lease lost.");
                }
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
