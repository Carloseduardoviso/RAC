namespace RAC.Api.Services;

public sealed class RacWorker(RacPersistentEngine engine, ILogger<RacWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try { await engine.TickAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception error)
                {
                    logger.LogCritical(error, "Simulador interrompido: erro na persistência ou regra de risco.");
                    break; // Never retry automatically after a storage failure.
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("RAC worker stopped.");
        }
    }
}
