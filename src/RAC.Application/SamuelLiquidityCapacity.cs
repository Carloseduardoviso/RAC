namespace RAC.Application;
public sealed record SamuelLiquidityScenario(decimal Quantity,SamuelRoundTripEstimate Estimate,string Status)
{
    public bool TradingAuthorized=>false;
}
public static class SamuelLiquidityCapacity
{
    public static IReadOnlyList<SamuelLiquidityScenario> Evaluate(SamuelDepthSnapshot book,
        IReadOnlyList<decimal> quantities,decimal feePercent,decimal maxImpactPercent,
        decimal maxCostPercent,DateTimeOffset now)
    {
        if(quantities.Count==0 || quantities.Count>100 || quantities.Any(q=>q<=0) ||
            quantities.Distinct().Count()!=quantities.Count ||
            maxImpactPercent<0 || maxImpactPercent>100 || maxCostPercent<0 || maxCostPercent>100)
            throw new ArgumentException("Invalid scenario inputs.");
        return quantities.OrderBy(q=>q).Select(q=>{
            var estimate=SamuelRoundTrip.Evaluate(book,q,feePercent,now);
            var status=estimate.NetReturnPercent is null?"INSUFFICIENT_DEPTH":
                estimate.Entry.ImpactPercent!.Value>maxImpactPercent ||
                estimate.Exit.ImpactPercent!.Value>maxImpactPercent?"EXCESSIVE_IMPACT":
                -estimate.NetReturnPercent.Value>maxCostPercent?"EXCESSIVE_COST":"RESEARCH_ONLY";
            return new SamuelLiquidityScenario(q,estimate,status);
        }).ToArray();
    }
}
