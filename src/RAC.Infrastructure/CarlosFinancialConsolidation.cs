using RAC.Application;
namespace RAC.Infrastructure;

// PAPER-only advisory assessment. No execution or account mutation.
public sealed record CarlosFinancialAssessment(RafaelCapitalAssessment? Capital,
    decimal? RealizedNetProfit, IReadOnlyList<string> Findings, string Status)
{
    public bool TradingAuthorized => false;
}
public static class CarlosFinancialConsolidation
{
    public static CarlosFinancialAssessment Evaluate(RafaelStoredCapital? data,
        DateTimeOffset now, TimeSpan maximumAge, decimal tolerance)
    {
        if(maximumAge<=TimeSpan.Zero || tolerance<0)
            throw new ArgumentException("Invalid assessment parameters.");
        if(data is null) return new(null,null,Array.AsReadOnly(new[]{"NO_PAPER_ACCOUNT"}),"REVIEW_REQUIRED");
        var capital=RafaelCapitalAnalysis.Evaluate(data.Snapshot);
        var health=RafaelFinancialHealth.Evaluate(data.Snapshot,data.StoredEquity,data.UpdatedUtc,
            data.LastCycleUtc,data.Running,now,maximumAge,tolerance);
        return new(capital,data.RealizedNetProfit,health.Findings,
            data.Snapshot.RiskLocked?"RISK_LOCKED":health.Findings.Count>0?"REVIEW_REQUIRED":"HUMAN_REVIEW_REQUIRED");
    }
}
