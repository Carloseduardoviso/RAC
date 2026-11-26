namespace RAC.Application;

public sealed record RafaelAgentEvidence(string AgentId,decimal ExistingExposure,decimal QualityScore,bool IndependentValidation);
public sealed record RafaelAgentAllocation(string AgentId,decimal ResearchAmount)
{
    public bool TradingAuthorized=>false;
}
public static class RafaelCapitalDistribution
{
    public static IReadOnlyList<RafaelAgentAllocation> Evaluate(RafaelCapitalSnapshot snapshot,
        RafaelAllocationLimits limits,IReadOnlyList<RafaelAgentEvidence> agents)
    {
        var capacity=RafaelAllocationCapacity.Evaluate(snapshot,limits);
        if(agents.Count>12 || agents.Select(x=>x.AgentId).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=agents.Count ||
            agents.Any(x=>string.IsNullOrWhiteSpace(x.AgentId)||x.AgentId!=x.AgentId.Trim()||x.ExistingExposure<0||x.QualityScore is <0 or >1) ||
            agents.Sum(x=>x.ExistingExposure)>snapshot.MarkedPositions)
            throw new ArgumentException("Invalid agent evidence.");
        var remaining=snapshot.RiskLocked?0:Math.Min(capacity.CashHeadroom,capacity.ExposureHeadroom);
        var result=new List<RafaelAgentAllocation>();
        foreach(var agent in agents.OrderByDescending(x=>x.QualityScore).ThenBy(x=>x.AgentId,StringComparer.Ordinal))
        {
            var amount=agent.IndependentValidation && agent.QualityScore>0
                ? Math.Min(remaining,Math.Max(0,capacity.SinglePositionCeiling-agent.ExistingExposure)):0;
            // Round down to eight decimal places to avoid allocating fractional excess.
            amount=decimal.Round(amount,8,MidpointRounding.ToZero);
            remaining-=amount;
            result.Add(new(agent.AgentId,amount));
        }
        return result;
    }
}
