using RAC.Application;

namespace RAC.Api;

// Not registered by default. Advisory-only cycle; no order or risk-state dependency.
public sealed class RacAnaAdvisoryWorker(
    Func<DateTimeOffset,TimeSpan,CancellationToken,Task<RacAnaAdvisoryResult>> review,
    ILogger<RacAnaAdvisoryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            do
            {
                try
                {
                    var result = await review(DateTimeOffset.UtcNow,TimeSpan.FromMinutes(15),stoppingToken);
                    logger.LogInformation("Ana advisory: {Status}; evidence count {Count}.",
                        result.Status,result.EvidenceIds.Count);
                }
                catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { return; }
                catch(Exception ex)
                {
                    logger.LogWarning("Ana advisory unavailable: {FailureType}.",ex.GetType().Name);
                }
            } while(await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { }
    }
}
