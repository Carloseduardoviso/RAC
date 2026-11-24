using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;
namespace RAC.Api.Services;
public sealed class RebecaResearchWorker(CrossrefResearchClient client,ResearchSourceCandidates candidates,ResearchSourceStore store,ResearchSourceReviewStore reviews,ResearchPublicationStore publications,ResearchExecutionStore history,ResearchProductivityReader productivity,ResearchKnowledgeStore knowledge,ILogger<RebecaResearchWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromDays(1));
        do
        {
            var elapsed=System.Diagnostics.Stopwatch.StartNew();
            var started=DateTimeOffset.UtcNow;
            var topic=ResearchTopicRotation.ForDate(started);
            var record=new ResearchExecutionRecord{Topic=topic,StartedUtc=started,Status="FAILED"};
            try
            {
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(TimeSpan.FromSeconds(30));
                await store.LoadAsync(candidates,deadline.Token);
                                try
                {
                    using var selectionDeadline=CancellationTokenSource.CreateLinkedTokenSource(deadline.Token);
                    selectionDeadline.CancelAfter(TimeSpan.FromSeconds(3));
                    var metrics=await productivity.ReadAsync(started,selectionDeadline.Token);
                    topic=ResearchAdaptiveSelection.Choose(started,metrics);
                }
                catch(Exception e) when(!deadline.IsCancellationRequested && (e is Microsoft.Data.SqlClient.SqlException or InvalidOperationException or OperationCanceledException))
                {logger.LogWarning("Research selection fallback: {Category}",e.GetType().Name);}
                record.Topic=topic;
                logger.LogInformation("Research topic selected: {Topic} {Mode}",topic,topic==ResearchTopicRotation.ForDate(started)?"BASELINE":"ADAPTIVE");
                var batch=await client.ReadAsync(candidates,deadline.Token,topic);
                logger.LogInformation("Research abstracts available: {Count}",batch.Abstracts.Count);
                topic=batch.Topic; record.Topic=topic; record.PublicationsFound=batch.Publications.Count; record.CandidateDomains=batch.Added;
                await store.SaveAsync(candidates,deadline.Token);
                var saved=await publications.SaveAsync(batch.Publications,DateTimeOffset.UtcNow,deadline.Token);
                record.PublicationsSaved=saved;
                var extracted=await knowledge.ExtractAsync(DateTimeOffset.UtcNow,deadline.Token);
                logger.LogInformation("Research title mentions stored: {Count}",extracted);
                await reviews.RecordPendingAsync(DateTimeOffset.UtcNow,deadline.Token);
                record.Status="COMPLETED";
                logger.LogInformation("Rebeca research completed: {Topic} {Found} publications found, {Saved} new publications, {Added} new candidate domains, {ElapsedMs} ms",topic,batch.Publications.Count,saved,batch.Added,elapsed.ElapsedMilliseconds);
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){return;}
            catch(Exception e) when(e is Microsoft.Data.SqlClient.SqlException or Microsoft.EntityFrameworkCore.DbUpdateException or HttpRequestException or IOException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException or OperationCanceledException)
            {record.ErrorCategory=e.GetType().Name; logger.LogWarning("Rebeca research unavailable: {Topic} {Category} after {ElapsedMs} ms",topic,e.GetType().Name,elapsed.ElapsedMilliseconds);}
            record.ElapsedMs=elapsed.ElapsedMilliseconds;
            try
            {
                using var historyDeadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
                historyDeadline.CancelAfter(TimeSpan.FromSeconds(5));
                await history.SaveAsync(record,historyDeadline.Token);
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){return;}
            catch(Exception e) when(e is Microsoft.Data.SqlClient.SqlException or Microsoft.EntityFrameworkCore.DbUpdateException or InvalidOperationException or OperationCanceledException)
            {logger.LogWarning("Research history unavailable: {Category}",e.GetType().Name);}
        }while(await timer.WaitForNextTickAsync(ct));
    }
}
