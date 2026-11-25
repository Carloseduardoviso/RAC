namespace RAC.Application;

// Advisory evidence inventory only; never authorizes execution.
public sealed record RacAnaReportReviewResult(Guid CorrelationId,
    IReadOnlyList<RacAgentMessage> Evidence, IReadOnlyList<string> Missing,
    IReadOnlyList<string> Conflicting, bool RiskNoticePresent) { public IReadOnlyList<Guid> RiskNoticeIds { get; init; } = []; }

public static class RacAnaReportReview
{
    public static RacAnaReportReviewResult Review(Guid correlationId,
        IEnumerable<RacAgentMessage> messages, DateTimeOffset now, TimeSpan maxAge)
    {
        if(correlationId == Guid.Empty || maxAge <= TimeSpan.Zero)
            throw new ArgumentException("Invalid review window.");
        string[] expected = ["rebeca", "carlos", "lucas"];
        var scoped = messages.Where(m => m.CorrelationId == correlationId &&
            m.Recipient == "ana" && m.Id != Guid.Empty &&
            m.CreatedUtc <= now && now - m.CreatedUtc <= maxAge &&
            !string.IsNullOrWhiteSpace(m.Content) && m.Content.Length <= 4096).ToArray();
        var reports = scoped.Where(m => m.Kind == RacMessageKind.Report &&
            expected.Contains(m.Sender)).ToArray();
        var conflicts = expected.Where(sender => reports.Where(m => m.Sender == sender)
            .Select(m => m.Content).Distinct(StringComparer.Ordinal).Skip(1).Any()).ToArray();
        var missing = expected.Where(sender => !reports.Any(m => m.Sender == sender)).ToArray();
        return new(correlationId, Array.AsReadOnly(reports.OrderBy(m => m.Sender, StringComparer.Ordinal)
            .ThenBy(m => m.CreatedUtc).ThenBy(m => m.Id).ToArray()),
            Array.AsReadOnly(missing), Array.AsReadOnly(conflicts),
            scoped.Any(m => m.Sender == "carlos" && m.Kind == RacMessageKind.RiskNotice)) { RiskNoticeIds = Array.AsReadOnly(scoped.Where(m => m.Sender == "carlos" && m.Kind == RacMessageKind.RiskNotice).Select(m => m.Id).Distinct().OrderBy(x => x).ToArray()) };
    }
}
