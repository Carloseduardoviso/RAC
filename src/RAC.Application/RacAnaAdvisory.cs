namespace RAC.Application;

public sealed record RacAnaAdvisoryResult(string Status, IReadOnlyList<string> Reasons,
    IReadOnlyList<Guid> EvidenceIds)
{
    public bool TradingAuthorized => false;
}

public static class RacAnaAdvisory
{
    public static RacAnaAdvisoryResult Summarize(IEnumerable<RacAnaReportReviewResult> reviews)
    {
        var batch = reviews.OrderBy(x => x.CorrelationId).ToArray();
        var reasons = new List<string>();
        if(batch.Length == 0) reasons.Add("NO_EVIDENCE");
        foreach(var review in batch)
        {
            if(review.RiskNoticePresent) reasons.Add($"{review.CorrelationId}:RISK_NOTICE");
            foreach(var sender in review.Missing.OrderBy(x => x, StringComparer.Ordinal))
                reasons.Add($"{review.CorrelationId}:MISSING:{sender}");
            foreach(var sender in review.Conflicting.OrderBy(x => x, StringComparer.Ordinal))
                reasons.Add($"{review.CorrelationId}:CONFLICT:{sender}");
        }
        var status = batch.Any(x => x.RiskNoticePresent) ? "RISK_REVIEW_REQUIRED" :
            reasons.Count > 0 ? "INCOMPLETE_REVIEW" : "HUMAN_REVIEW_REQUIRED";
        if(reasons.Count == 0) reasons.Add("SEMANTIC_REVIEW_REQUIRED");
        return new(status, reasons.AsReadOnly(), Array.AsReadOnly(batch.SelectMany(x => x.Evidence)
            .Select(x => x.Id).Distinct().OrderBy(x => x).ToArray()));
    }
}
