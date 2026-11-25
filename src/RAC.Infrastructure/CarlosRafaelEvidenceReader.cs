using RAC.Domain;
using RAC.Application;
namespace RAC.Infrastructure;

// Read-only evidence inventory; never interprets report content as authorization.
public sealed record CarlosRafaelEvidence(Guid TaskId, Guid MessageId, DateTimeOffset ObservedUtc, string Content)
{
    public bool TradingAuthorized => false;
}
public sealed class CarlosRafaelEvidenceReader(RacAgentMessageStore messages)
{
    public async Task<IReadOnlyList<CarlosRafaelEvidence>> ReadAsync(Guid taskId,
        DateTimeOffset now, TimeSpan maximumAge, CancellationToken ct)
    {
        if(taskId==Guid.Empty || maximumAge<=TimeSpan.Zero)
            throw new ArgumentException("Invalid evidence window.");
        var rows=await messages.ReadInboxAsync("carlos",taskId,500,ct);
        if(rows.Count>=500) throw new InvalidOperationException("Evidence page may be incomplete.");
        return Select(taskId,rows,now,maximumAge);
    }
    public static IReadOnlyList<CarlosRafaelEvidence> Select(Guid taskId,
        IEnumerable<RacAgentMessageRecord> rows,DateTimeOffset now,TimeSpan maximumAge)
    {
        if(taskId==Guid.Empty || maximumAge<=TimeSpan.Zero)
            throw new ArgumentException("Invalid evidence window.");
        return Array.AsReadOnly(rows.Where(x=>x.CorrelationId==taskId && x.Id!=Guid.Empty &&
            x.Sender=="rafael" && x.Recipient=="carlos" && x.Kind=="Report" &&
            x.CreatedUtc<=now && now-x.CreatedUtc<=maximumAge &&
            !string.IsNullOrWhiteSpace(x.Content) && x.Content.Length<=4096)
            .OrderBy(x=>x.CreatedUtc).ThenBy(x=>x.Id)
            .Select(x=>new CarlosRafaelEvidence(taskId,x.Id,x.CreatedUtc,x.Content)).ToArray());
    }
}
