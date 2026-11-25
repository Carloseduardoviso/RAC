using RAC.Application;

namespace RAC.Infrastructure;

// Advisory-only composition. A failed write fails the cycle rather than reporting success.
public sealed class RacAnaAdvisoryCycle(RacAnaRecentReview review, RacAnaAdvisoryStore store)
{
    public async Task<RacAnaAdvisoryResult> RunAsync(DateTimeOffset now, TimeSpan maxAge, CancellationToken ct)
    {
        var result = await review.ReviewAsync(now,maxAge,ct);
        await store.SaveAsync(result,now,ct);
        return result;
    }
}
