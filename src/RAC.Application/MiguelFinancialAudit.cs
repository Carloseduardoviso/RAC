namespace RAC.Application;
public sealed record MiguelEquityAudit(decimal CalculatedEquity,decimal RecordedEquity,
    decimal Difference,decimal NetResult,IReadOnlyList<string> Findings)
{
    public bool Consistent=>Findings.Count==0;
    public bool TradingAuthorized=>false;
}
public static class MiguelFinancialAudit
{
    public static MiguelEquityAudit Evaluate(decimal cash,IReadOnlyList<decimal> markedPositions,
        decimal contributions,decimal recordedEquity,decimal tolerance)
    {
        if(tolerance<0 || markedPositions.Count>10000)
            throw new ArgumentException("Invalid audit parameters.");
        var findings=new List<string>();
        if(cash<0) findings.Add("NEGATIVE_CASH");
        if(markedPositions.Any(p=>p<0)) findings.Add("NEGATIVE_POSITION_VALUE");
        if(recordedEquity<0) findings.Add("NEGATIVE_RECORDED_EQUITY");
        var calculated=cash+markedPositions.Sum();
        var difference=calculated-recordedEquity;
        if(Math.Abs(difference)>tolerance) findings.Add("EQUITY_MISMATCH");
        return new(calculated,recordedEquity,difference,calculated-contributions,findings.AsReadOnly());
    }
}
