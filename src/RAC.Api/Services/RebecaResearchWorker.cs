using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Api.Services;
public sealed class RebecaResearchWorker(CrossrefResearchClient client,ResearchSourceCandidates candidates,ResearchSourceStore store,ILogger<RebecaResearchWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromDays(1));
        do
        {
            try
            {
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(TimeSpan.FromSeconds(30));
                await store.LoadAsync(candidates,deadline.Token);
                var added=await client.ReadAsync(candidates,deadline.Token);
                await store.SaveAsync(candidates,deadline.Token);
                logger.LogInformation("Rebeca research: {Added} new candidate domains",added);
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){return;}
            catch(Exception e) when(e is Microsoft.Data.SqlClient.SqlException or Microsoft.EntityFrameworkCore.DbUpdateException or HttpRequestException or IOException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException or OperationCanceledException)
            {logger.LogWarning("Rebeca research unavailable: {Category}",e.GetType().Name);}
        }while(await timer.WaitForNextTickAsync(ct));
    }
}
