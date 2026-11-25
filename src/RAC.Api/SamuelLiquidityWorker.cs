using RAC.Infrastructure;
namespace RAC.Api;

public sealed class SamuelLiquidityWorker(SamuelTaskExecutor executor,ILogger<SamuelLiquidityWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(30));
        try
        {
            do
            {
                try
                {
                    if(await executor.ExecuteNextAsync(stoppingToken))
                        logger.LogInformation("Samuel liquidity task processed.");
                }
                catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { return; }
                catch(Exception ex)
                {
                    logger.LogWarning("Samuel liquidity cycle failed: {FailureType}. Pending recovery uses task lease.",ex.GetType().Name);
                }
            } while(await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { }
    }
}
