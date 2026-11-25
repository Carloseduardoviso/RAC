namespace RAC.Application;

public sealed record RafaelCapitalSnapshot(decimal Cash,decimal MarkedPositions,decimal Contributions,bool RiskLocked);
public sealed record RafaelCapitalAssessment(decimal Equity,decimal? ExposurePercent,decimal? CashPercent,
    decimal NetResult,string Status)
{
    public bool TradingAuthorized=>false;
}
public static class RafaelCapitalAnalysis
{
    public static RafaelCapitalAssessment Evaluate(RafaelCapitalSnapshot snapshot)
    {
        if(snapshot.Cash<0 || snapshot.MarkedPositions<0 || snapshot.Contributions<0)
            throw new ArgumentException("Invalid capital snapshot.");
        var equity=snapshot.Cash+snapshot.MarkedPositions;
        return new(equity,equity==0?null:snapshot.MarkedPositions/equity*100,
            equity==0?null:snapshot.Cash/equity*100,equity-snapshot.Contributions,
            snapshot.RiskLocked?"RISK_LOCKED":equity==0?"NO_CAPITAL":"ANALYSIS_ONLY");
    }
}
