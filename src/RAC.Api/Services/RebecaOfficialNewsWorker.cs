using RAC.Infrastructure;
namespace RAC.Api.Services;

// Observation only. Headlines never become orders or verified calendar events.
public sealed class RebecaOfficialNewsWorker(FederalReserveNewsClient client,ILogger<RebecaOfficialNewsWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try
            {
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(TimeSpan.FromSeconds(20));
                var articles=await client.ReadAsync(deadline.Token);
                logger.LogInformation("Rebeca FED observation: {Count} recent articles",articles.Count);
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){return;}
            catch(Exception e) when(e is HttpRequestException or IOException or System.Xml.XmlException or OperationCanceledException)
            {logger.LogWarning("Rebeca FED unavailable: {Category}",e.GetType().Name);}
        }while(await timer.WaitForNextTickAsync(ct));
    }
}
