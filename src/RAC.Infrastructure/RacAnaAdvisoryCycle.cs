using RAC.Application;

namespace RAC.Infrastructure;

// Advisory-only composition. A failed write fails the cycle rather than reporting success.
public sealed class RacAnaAdvisoryCycle(RacAnaRecentReview review, RacAnaAdvisoryStore store, RacAnaReviewRequestStore? requests = null)
{
    public async Task<RacAnaAdvisoryResult> RunAsync(DateTimeOffset now, TimeSpan maxAge, CancellationToken ct)
    {
        var result = await review.ReviewAsync(now,maxAge,ct);
        await store.SaveAsync(result,now,ct);
        if(requests is not null && result.EvidenceIds.Count>0) await requests.SaveAsync(result,now,ct);
        return result;
    }
}
