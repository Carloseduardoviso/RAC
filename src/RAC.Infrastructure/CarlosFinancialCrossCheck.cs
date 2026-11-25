using System.Text.Json;
namespace RAC.Infrastructure;
public static class CarlosFinancialCrossCheck
{
    public static IReadOnlyList<string> Review(CarlosRafaelEvidence r,CarlosMiguelEvidence m,decimal tolerance)
    {
        if(tolerance<0) throw new ArgumentOutOfRangeException(nameof(tolerance));
        if(CarlosRafaelReportReview.Review(r).Capital is null ||
            CarlosMiguelReportReview.Review(m).Status=="INVALID_REPORT") return ["CROSS_CHECK_UNAVAILABLE"];
        try
        {
            using var rd=JsonDocument.Parse(r.Content);
            using var md=JsonDocument.Parse(m.Content);
            var a=rd.RootElement; var b=md.RootElement;
            var updated=a.GetProperty("UpdatedUtc").GetDateTimeOffset();
            var cycle=a.GetProperty("LastCycleUtc").GetDateTimeOffset();
            if(updated!=b.GetProperty("UpdatedUtc").GetDateTimeOffset() ||
                cycle!=b.GetProperty("LastCycleUtc").GetDateTimeOffset() ||
                cycle>updated || updated>r.ObservedUtc || updated>m.ObservedUtc)
                return ["SNAPSHOT_NOT_COMPARABLE"];
            var findings=new List<string>();
            void Compare(string left,string right,string code)
            {
                if(Math.Abs(a.GetProperty(left).GetDecimal()-b.GetProperty(right).GetDecimal())>tolerance)
                    findings.Add(code);
            }
            Compare("Equity","CalculatedEquity","EQUITY_MISMATCH");
            Compare("NetResult","NetResult","NET_RESULT_MISMATCH");
            Compare("RealizedNetProfit","Realized","REALIZED_MISMATCH");
            if(a.GetProperty("PositionCount").GetInt32()!=b.GetProperty("PositionCount").GetInt32()) findings.Add("POSITION_COUNT_MISMATCH");
            if(a.GetProperty("FillCount").GetInt32()!=b.GetProperty("FillCount").GetInt32()) findings.Add("FILL_COUNT_MISMATCH");
            if(a.GetProperty("RiskLocked").GetBoolean()!=b.GetProperty("RiskLocked").GetBoolean()) findings.Add("RISK_STATE_MISMATCH");
            return findings.AsReadOnly();
        }
        catch(Exception ex) when(ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException or ArithmeticException)
        {return ["CROSS_CHECK_UNAVAILABLE"];}
    }
}
