namespace RAC.Application;

// Recommendations are read-only; no automatic parameter changes or execution.
public static class IndependentResearchRecommendations
{
    public sealed record Report(IReadOnlyList<string> Actions)
    {
        public bool TradingAuthorized=>false;
    }
    public static Report Evaluate(IReadOnlyList<IndependentResearchGate.Evidence> evidence,DateTimeOffset asOf)
    {
        var classification=IndependentResearchClassification.Evaluate(evidence,asOf);
        var deterioration=IndependentResearchDeterioration.Evaluate(evidence);
        var actions=new List<string>();
        if(classification.Status=="INVALID_EVIDENCE" || deterioration.Status=="INVALID_EVIDENCE")
            actions.Add("AUDIT_EVIDENCE_INTEGRITY");
        else
        {
            if(classification.Status=="VALIDATION_PENDING")actions.Add("COLLECT_COMPLETE_INDEPENDENT_EVIDENCE");
            if(classification.Reasons.Contains("INSUFFICIENT_TRADES"))actions.Add("REVIEW_SAMPLE_SUFFICIENCY");
            if(classification.Reasons.Contains("INSUFFICIENT_PROFIT_FACTOR") || classification.Reasons.Contains("INSUFFICIENT_POSITIVE_MONTHS") || deterioration.Reasons.Contains("CONSECUTIVE_NEGATIVE_MONTHS"))
                actions.Add("REVIEW_NET_PERFORMANCE_AND_COSTS");
            if(classification.Reasons.Contains("RISK_OR_UNRESOLVED_POSITION") || deterioration.Reasons.Contains("RISK_REVIEW_REQUIRED"))
                actions.Add("REQUEST_CARLOS_RISK_REVIEW");
            if(classification.Status=="HUMAN_REVIEW_REQUIRED" && deterioration.Status=="NO_ALERT")actions.Add("REQUEST_ANA_HUMAN_REVIEW");
        }
        return new(Array.AsReadOnly(actions.Distinct().ToArray()));
    }
}
