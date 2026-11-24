namespace RAC.Application;
public static class ResearchLeadershipReview
{
    public sealed record Metrics(decimal StressMeanPct,decimal EvaluationMeanPct,int PositiveMonths,int Trades,decimal MaxDrawdownPct,int Unresolved);
    public sealed record Decision(string Rebeca,string Carlos,string Ana,IReadOnlyList<string> Reasons,bool TradingAuthorized);
    public static Decision Evaluate(Metrics m)
    {
        var reasons=new List<string>();
        if(m.Trades<30)reasons.Add("INSUFFICIENT_TRADES");
        if(m.PositiveMonths<4||m.PositiveMonths>6)reasons.Add("INSUFFICIENT_CONSISTENCY");
        if(m.StressMeanPct<=0||m.EvaluationMeanPct<=0)reasons.Add("NONPOSITIVE_NET_RETURN");
        if(m.Unresolved!=0)reasons.Add("UNRESOLVED_POSITIONS");
        if(m.MaxDrawdownPct<0||m.MaxDrawdownPct>3)reasons.Add("DRAWDOWN_LIMIT");
        return new("INDEPENDENT_VALIDATION_REQUIRED",reasons.Count==0?"RESEARCH_SCREEN_PASS":"RESEARCH_SCREEN_FAIL",reasons.Count==0?"QUEUE_INDEPENDENT_RESEARCH":"REJECT_CURRENT_CONFIGURATION",reasons,false);
    }
}
