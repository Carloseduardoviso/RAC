using RAC.Application;

namespace RAC.Infrastructure;

// Read-only adapter. A full inbox page is rejected rather than silently omitting evidence.
public sealed class RacAnaInboxReview(RacAgentMessageStore messages)
{
    public async Task<RacAnaReportReviewResult> ReviewAsync(Guid correlationId,
        DateTimeOffset now, TimeSpan maxAge, CancellationToken ct)
    {
        if(correlationId == Guid.Empty || maxAge <= TimeSpan.Zero)
            throw new ArgumentException("Invalid review window.");
        var rows = await messages.ReadInboxAsync("ana", correlationId, 500, ct);
        if(rows.Count >= 500)
            throw new InvalidOperationException("Inbox review may be incomplete.");
        var evidence = rows.Select(row => new RacAgentMessage(row.Id, row.CorrelationId,
            row.Sender, row.Recipient,
            row.Kind switch
            {
                "Report" => RacMessageKind.Report,
                "RiskNotice" => RacMessageKind.RiskNotice,
                "TaskRequest" => RacMessageKind.TaskRequest,
                _ => throw new InvalidOperationException("Unknown stored message kind.")
            }, row.Content, row.CreatedUtc));
        return RacAnaReportReview.Review(correlationId, evidence, now, maxAge);
    }
}
