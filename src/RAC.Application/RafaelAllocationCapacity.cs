namespace RAC.Application;

public sealed record RafaelAllocationLimits(decimal ReservePercent,decimal MaxExposurePercent,decimal MaxPositionPercent);
public sealed record RafaelAllocationAssessment(decimal Equity,decimal ReserveRequired,decimal ExposureHeadroom,
    decimal CashHeadroom,decimal SinglePositionCeiling,decimal ResearchCapacity,string Status)
{
    public bool TradingAuthorized=>false;
}
public static class RafaelAllocationCapacity
{
    public static RafaelAllocationAssessment Evaluate(RafaelCapitalSnapshot snapshot,RafaelAllocationLimits limits)
    {
        if(limits.ReservePercent is <0 or >100 || limits.MaxExposurePercent is <0 or >100 ||
            limits.MaxPositionPercent is <0 or >100) throw new ArgumentException("Invalid allocation limits.");
        var capital=RafaelCapitalAnalysis.Evaluate(snapshot);
        var equity=capital.Equity;
        var reserve=equity*(limits.ReservePercent/100m);
        var exposure=Math.Max(0,equity*(limits.MaxExposurePercent/100m)-snapshot.MarkedPositions);
        var cash=Math.Max(0,snapshot.Cash-reserve);
        var position=equity*(limits.MaxPositionPercent/100m);
        var capacity=snapshot.RiskLocked?0:Math.Min(position,Math.Min(exposure,cash));
        return new(equity,reserve,exposure,cash,position,capacity,
            snapshot.RiskLocked?"RISK_LOCKED":capacity==0?"NO_CAPACITY":"ANALYSIS_ONLY");
    }
}
