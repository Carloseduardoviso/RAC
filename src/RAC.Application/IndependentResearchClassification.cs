namespace RAC.Application;

// Classification is advisory only and never authorizes trading or capital allocation.
public static class IndependentResearchClassification
{
    public sealed record Assessment(string Status,IReadOnlyList<string> Reasons)
    {
        public bool TradingAuthorized=>false;
    }
    public static Assessment Evaluate(IReadOnlyList<IndependentResearchGate.Evidence> evidence,DateTimeOffset asOf)
    {
        var reasons=IndependentResearchGate.Evaluate(evidence,asOf);
        var status=reasons.Contains("INVALID_METRICS")?"INVALID_EVIDENCE":
            reasons.Contains("INCOMPLETE_OR_DUPLICATE_EVIDENCE")||reasons.Contains("VALIDATION_PERIOD_NOT_COMPLETE")?"VALIDATION_PENDING":
            reasons.Length>0?"RESEARCH_REJECTED":"HUMAN_REVIEW_REQUIRED";
        return new(status,Array.AsReadOnly(reasons));
    }
}
