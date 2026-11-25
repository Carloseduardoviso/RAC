namespace RAC.Application;
public sealed record MiguelAuditFinding(string Code,string Severity);
public sealed record MiguelConsolidatedAudit(MiguelEquityAudit Equity,MiguelMovementAudit Movements,
    MiguelPositionAudit Positions,decimal? ProfitDifference,IReadOnlyList<MiguelAuditFinding> Findings)
{
    public bool Consistent=>Findings.Count==0;
    public bool TradingAuthorized=>false;
}
public static class MiguelIntegrityAudit
{
    public static MiguelConsolidatedAudit Evaluate(decimal cash,decimal contributions,decimal recordedEquity,
        IReadOnlyList<MiguelFill> fills,IReadOnlyList<MiguelOpenPosition> positions,
        decimal moneyTolerance,decimal quantityTolerance)
    {
        var equity=MiguelFinancialAudit.Evaluate(cash,positions.Select(p=>p.Quantity*p.LastPrice).ToArray(),
            contributions,recordedEquity,moneyTolerance);
        var movements=MiguelMovementReconciliation.Evaluate(fills,contributions,cash,moneyTolerance);
        var portfolio=MiguelPositionReconciliation.Evaluate(fills,positions,moneyTolerance,quantityTolerance);
        var findings=equity.Findings.Concat(movements.Findings).Concat(portfolio.Findings)
            .Distinct(StringComparer.Ordinal).Select(x=>new MiguelAuditFinding(x,"CRITICAL")).ToList();
        if(contributions<0) findings.Add(new("NEGATIVE_CONTRIBUTIONS","CRITICAL"));
        decimal? difference=null;
        if(movements.CalculatedRealized is decimal realized && portfolio.Unrealized is decimal unrealized)
        {
            difference=equity.NetResult-realized-unrealized;
            if(Math.Abs(difference.Value)>moneyTolerance)
                findings.Add(new("PROFIT_RECONCILIATION_MISMATCH","CRITICAL"));
        }
        else findings.Add(new("PROFIT_RECONCILIATION_UNAVAILABLE","CRITICAL"));
        return new(equity,movements,portfolio,difference,findings.AsReadOnly());
    }
}
