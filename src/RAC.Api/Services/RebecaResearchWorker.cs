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
            var elapsed=System.Diagnostics.Stopwatch.StartNew();
            var topic=ResearchTopicRotation.ForDate(DateTimeOffset.UtcNow);
            try
            {
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(TimeSpan.FromSeconds(30));
                await store.LoadAsync(candidates,deadline.Token);
                var batch=await client.ReadAsync(candidates,deadline.Token);
                topic=batch.Topic;
                await store.SaveAsync(candidates,deadline.Token);
                var saved=await publications.SaveAsync(batch.Publications,DateTimeOffset.UtcNow,deadline.Token);
                await reviews.RecordPendingAsync(DateTimeOffset.UtcNow,deadline.Token);
                logger.LogInformation("Rebeca research completed: {Topic} {Found} publications found, {Saved} new publications, {Added} new candidate domains, {ElapsedMs} ms",topic,batch.Publications.Count,saved,batch.Added,elapsed.ElapsedMilliseconds);
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){return;}
            catch(Exception e) when(e is Microsoft.Data.SqlClient.SqlException or Microsoft.EntityFrameworkCore.DbUpdateException or HttpRequestException or IOException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException or OperationCanceledException)
            {logger.LogWarning("Rebeca research unavailable: {Topic} {Category} after {ElapsedMs} ms",topic,e.GetType().Name,elapsed.ElapsedMilliseconds);}
        }while(await timer.WaitForNextTickAsync(ct));
    }
}
