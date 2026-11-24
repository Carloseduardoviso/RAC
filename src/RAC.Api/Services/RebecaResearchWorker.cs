using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Api.Services;
public sealed class RebecaResearchWorker(CrossrefResearchClient client,ResearchSourceCandidates candidates,ResearchSourceStore store,ResearchSourceReviewStore reviews,ResearchPublicationStore publications,ILogger<RebecaResearchWorker> logger):BackgroundService
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
                var batch=await client.ReadAsync(candidates,deadline.Token);
                await store.SaveAsync(candidates,deadline.Token);
                var saved=await publications.SaveAsync(batch.Publications,DateTimeOffset.UtcNow,deadline.Token);
                await reviews.RecordPendingAsync(DateTimeOffset.UtcNow,deadline.Token);
                logger.LogInformation("Rebeca research: {Added} new candidate domains; {Saved} new publications",batch.Added,saved);
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){return;}
            catch(Exception e) when(e is Microsoft.Data.SqlClient.SqlException or Microsoft.EntityFrameworkCore.DbUpdateException or HttpRequestException or IOException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException or OperationCanceledException)
            {logger.LogWarning("Rebeca research unavailable: {Category}",e.GetType().Name);}
        }while(await timer.WaitForNextTickAsync(ct));
    }
}
