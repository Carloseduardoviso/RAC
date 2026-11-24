namespace RAC.Application;

// Evidence rubric, not a trading authorization or an automated fact checker.
public static class ResearchSourceReliability
{
    public sealed record Evidence(bool? Provenance,bool? Current,bool? Independent,bool? Transparent,bool? Reproducible,bool ConflictDisclosed);
    public sealed record Assessment(int Score,string Status,int UnknownCriteria,bool TradingAuthorized);
    public static Assessment Evaluate(Evidence evidence)
    {
        var criteria=new (bool? Value,int Weight)[]{
            (evidence.Provenance,30),(evidence.Current,15),(evidence.Independent,20),
            (evidence.Transparent,15),(evidence.Reproducible,20)};
        var score=criteria.Where(x=>x.Value==true).Sum(x=>x.Weight);
        var unknown=criteria.Count(x=>x.Value is null);
        var status=evidence.Provenance==false?"REJECTED":
            unknown>0||!evidence.ConflictDisclosed?"INSUFFICIENT_EVIDENCE":
            score>=80?"REVIEW_CANDIDATE":"LOW_CONFIDENCE";
        return new(score,status,unknown,false);
    }
}
