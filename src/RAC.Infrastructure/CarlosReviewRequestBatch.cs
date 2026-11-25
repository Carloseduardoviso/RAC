using Microsoft.EntityFrameworkCore;
namespace RAC.Infrastructure;

// Bounded explicit cycle. Not registered as a hosted worker.
public sealed class CarlosReviewRequestBatch(IDbContextFactory<RacDbContext> factory,CarlosReviewRequestPublisher publisher)
{
    public async Task<int> RunAsync(DateTimeOffset now,TimeSpan maximumAge,CancellationToken ct)
    {
        if(maximumAge<=TimeSpan.Zero || maximumAge>TimeSpan.FromDays(1)) throw new ArgumentException("Invalid review window.");
        await using var db=await factory.CreateDbContextAsync(ct);
        var since=now-maximumAge;
        var ids=await db.AgentMessages.AsNoTracking().Where(x=>x.Sender=="ana" && x.Recipient=="carlos" && x.Kind=="TaskRequest" && x.CreatedUtc>=since && x.CreatedUtc<=now &&
            !db.AgentMessages.Any(y=>y.CorrelationId==x.CorrelationId && y.Sender=="carlos" && y.Recipient=="ana" && (y.Kind=="Report" || y.Kind=="RiskNotice")))
            .OrderBy(x=>x.CreatedUtc).ThenBy(x=>x.Id).Select(x=>x.Id).Take(101).ToListAsync(ct);
        if(ids.Count>100) throw new InvalidOperationException("Financial request batch overflow.");
        var count=0;
        foreach(var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            if(await publisher.PublishAsync(id,now,maximumAge,ct) is not null) count++;
        }
        return count;
    }
}
