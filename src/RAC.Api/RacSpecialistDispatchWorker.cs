using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAC.Infrastructure;

namespace RAC.Api;

// Intentionally not registered until specialist handlers and database migrations are ready.
public sealed class RacSpecialistDispatchWorker(
    RacSpecialistDispatcher dispatcher,
    RacAgentTaskStore tasks,
    ILogger<RacSpecialistDispatchWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try
            {
                var recovered = await tasks.RecoverExpiredAsync(stoppingToken);
                var dispatched = await dispatcher.DispatchAsync(DateTimeOffset.UtcNow, stoppingToken);
                if (recovered > 0 || dispatched > 0)
                    logger.LogInformation("Specialist queue: recovered {Recovered}, dispatched {Dispatched}", recovered, dispatched);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Specialist dispatch cycle failed: {ExceptionType}", ex.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
