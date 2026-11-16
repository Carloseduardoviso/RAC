using RAC.Api.Models;

namespace RAC.Api.Services;

public sealed class RacWorker(RacSimulator sim, ILogger<RacWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try { if (sim.Snapshot().Running) sim.Tick(); }
                catch (Exception e) { logger.LogError(e, "Erro no simulador RAC"); sim.Pause(); }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { logger.LogInformation("RAC worker stopped."); }
    }
}

