namespace RAC.Application;

public sealed record RafaelGrowthSnapshot(decimal Cash,decimal MarkedPositions,decimal OpenCostBasis,
    decimal NetContributions,decimal RealizedNetProfit,bool RiskLocked);
public sealed record RafaelGrowthAssessment(decimal Equity,decimal UnrealizedProfit,decimal TotalNetProfit,
    decimal ReconciliationDifference,decimal? NetProfitToContributionsPercent,decimal ResearchReinvestmentCeiling,string Status)
{
    public bool TradingAuthorized=>false;
}
public static class RafaelCapitalGrowth
{
    public static RafaelGrowthAssessment Evaluate(RafaelGrowthSnapshot snapshot,decimal reinvestmentPercent)
    {
        if(snapshot.Cash<0 || snapshot.MarkedPositions<0 || snapshot.OpenCostBasis<0 ||
            reinvestmentPercent is <0 or >100) throw new ArgumentException("Invalid growth inputs.");
        var equity=snapshot.Cash+snapshot.MarkedPositions;
        var unrealized=snapshot.MarkedPositions-snapshot.OpenCostBasis;
        var profit=equity-snapshot.NetContributions;
        var difference=profit-snapshot.RealizedNetProfit-unrealized;
        var reconciled=difference==0;
        var ceiling=snapshot.RiskLocked || !reconciled?0:
            Math.Min(snapshot.Cash,Math.Min(Math.Max(0,profit),Math.Max(0,snapshot.RealizedNetProfit))*(reinvestmentPercent/100m));
        return new(equity,unrealized,profit,difference,
            snapshot.NetContributions>0?profit/snapshot.NetContributions*100:null,ceiling,
            !reconciled?"RECONCILIATION_REQUIRED":snapshot.RiskLocked?"RISK_LOCKED":"ANALYSIS_ONLY");
    }
}
