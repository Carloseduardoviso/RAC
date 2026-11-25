namespace RAC.Application;

public sealed record RafaelFinancialHealthResult(IReadOnlyList<string> Findings,bool DataConsistent,bool ResearchAllocationEligible)
{
    public bool TradingAuthorized=>false;
}
public static class RafaelFinancialHealth
{
    public static RafaelFinancialHealthResult Evaluate(RafaelCapitalSnapshot snapshot,decimal storedEquity,
        DateTimeOffset updatedUtc,DateTimeOffset? lastCycleUtc,bool running,DateTimeOffset now,
        TimeSpan maximumAge,decimal reconciliationTolerance)
    {
        if(maximumAge<=TimeSpan.Zero || reconciliationTolerance<0 || storedEquity<0)
            throw new ArgumentException("Invalid health assessment parameters.");
        var capital=RafaelCapitalAnalysis.Evaluate(snapshot);
        var findings=new List<string>();
        if(updatedUtc>now || now-updatedUtc>maximumAge) findings.Add("ACCOUNT_TIMESTAMP_INVALID_OR_STALE");
        if(lastCycleUtc is null || lastCycleUtc>now || now-lastCycleUtc>maximumAge) findings.Add("CYCLE_TIMESTAMP_INVALID_OR_STALE");
        if(Math.Abs(capital.Equity-storedEquity)>reconciliationTolerance) findings.Add("EQUITY_MISMATCH");
        var consistent=findings.Count==0;
        if(snapshot.RiskLocked) findings.Add("RISK_LOCKED");
        if(!running) findings.Add("PAPER_STOPPED");
        if(snapshot.Cash<=0) findings.Add("NO_AVAILABLE_CASH");
        return new(findings.AsReadOnly(),consistent,findings.Count==0);
    }
}
