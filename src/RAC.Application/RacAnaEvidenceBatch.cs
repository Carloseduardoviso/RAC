namespace RAC.Application;

// Explicit task selection preserves provenance; this is not a trading decision.
public static class RacAnaEvidenceBatch
{
    public static IReadOnlyList<RacAnaReportReviewResult> Review(
        IEnumerable<Guid> taskIds, IEnumerable<RacAgentMessage> messages,
        DateTimeOffset now, TimeSpan maxAge)
    {
        var ids = taskIds.Distinct().OrderBy(x => x).ToArray();
        if(ids.Length is < 1 or > 100 || ids.Contains(Guid.Empty) || maxAge <= TimeSpan.Zero)
            throw new ArgumentException("Invalid evidence batch.");
        var evidence = messages.ToArray();
        return Array.AsReadOnly(ids.Select(id =>
            RacAnaReportReview.Review(id, evidence, now, maxAge)).ToArray());
    }
}
