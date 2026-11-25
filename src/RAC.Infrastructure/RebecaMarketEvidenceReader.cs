using RAC.Domain;
namespace RAC.Infrastructure;
public sealed record RebecaMarketEvidence(Guid TaskId,Guid MessageId,string Specialist,DateTimeOffset ObservedUtc,string Content)
{
    public bool TradingAuthorized=>false;
}
public sealed class RebecaMarketEvidenceReader(RacAgentMessageStore messages)
{
    public async Task<IReadOnlyList<RebecaMarketEvidence>> ReadAsync(string specialist,Guid taskId,DateTimeOffset now,TimeSpan maximumAge,CancellationToken ct)
    {
        Validate(specialist,taskId,maximumAge);
        var rows=await messages.ReadInboxAsync("rebeca",taskId,500,ct);
        if(rows.Count>=500) throw new InvalidOperationException("Evidence page may be incomplete.");
        return Select(specialist,taskId,rows,now,maximumAge);
    }
    private static void Validate(string specialist,Guid taskId,TimeSpan maximumAge)
    {
        if(specialist is not ("daniel" or "gabriel") || taskId==Guid.Empty ||
            maximumAge<=TimeSpan.Zero || maximumAge>TimeSpan.FromDays(1))
            throw new ArgumentException("Invalid market evidence request.");
    }
    public static IReadOnlyList<RebecaMarketEvidence> Select(string specialist,Guid taskId,
        IEnumerable<RacAgentMessageRecord> rows,DateTimeOffset now,TimeSpan maximumAge)
    {
        Validate(specialist,taskId,maximumAge);
        return Array.AsReadOnly(rows.Where(x=>x.CorrelationId==taskId && x.Id!=Guid.Empty &&
            x.Sender==specialist && x.Recipient=="rebeca" && x.Kind=="Report" &&
            x.CreatedUtc<=now && now-x.CreatedUtc<=maximumAge &&
            !string.IsNullOrWhiteSpace(x.Content) && x.Content.Length<=4096)
            .OrderBy(x=>x.CreatedUtc).ThenBy(x=>x.Id)
            .Select(x=>new RebecaMarketEvidence(taskId,x.Id,specialist,x.CreatedUtc,x.Content)).ToArray());
    }
}
