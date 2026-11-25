using RAC.Application;

namespace RAC.Infrastructure;

// Explicit, read-only review. No execution or risk-state dependencies.
public sealed class RacAnaAdvisoryService(RacAnaInboxReview inbox)
{
    public async Task<RacAnaAdvisoryResult> ReviewAsync(IEnumerable<Guid> taskIds,
        DateTimeOffset now, TimeSpan maxAge, CancellationToken ct)
    {
        var ids = taskIds.Distinct().OrderBy(x => x).ToArray();
        if(ids.Length is < 1 or > 100 || ids.Contains(Guid.Empty) || maxAge <= TimeSpan.Zero)
            throw new ArgumentException("Invalid evidence batch.");
        var reviews = new List<RacAnaReportReviewResult>();
        foreach(var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            reviews.Add(await inbox.ReviewAsync(id, now, maxAge, ct));
        }
        return RacAnaAdvisory.Summarize(reviews);
    }
}
