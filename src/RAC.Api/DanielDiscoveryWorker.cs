using RAC.Infrastructure;

namespace RAC.Api;

public sealed class DanielDiscoveryWorker(DanielTaskExecutor executor,ILogger<DanielDiscoveryWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                if(await executor.ExecuteNextAsync(stoppingToken))
                    logger.LogInformation("Daniel research task processed.");
            }
            catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch(Exception ex)
            {
                logger.LogWarning("Daniel research cycle failed: {FailureType}. Pending recovery uses task lease.",ex.GetType().Name);
            }
        } while(await timer.WaitForNextTickAsync(stoppingToken));
    }
}
