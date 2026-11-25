using System.Text.Json;
namespace RAC.Infrastructure;
public sealed record CarlosSamuelReportResult(Guid MessageId,string? Symbol,IReadOnlyList<string> Findings,string Status)
{
    public bool TradingAuthorized=>false;
}
public static class CarlosSamuelReportReview
{
    public static CarlosSamuelReportResult Review(CarlosSamuelEvidence evidence)
    {
        CarlosSamuelReportResult Invalid()=>new(evidence.MessageId,null,[],"INVALID_REPORT");
        try
        {
            if(evidence.MessageId==Guid.Empty || evidence.TaskId==Guid.Empty || evidence.Content.Length>4096) return Invalid();
            using var doc=JsonDocument.Parse(evidence.Content);
            var r=doc.RootElement;
            if(!Unique(r) || r.GetProperty("Mode").GetString()!="RESEARCH_ONLY" ||
                r.GetProperty("TradingAuthorized").ValueKind!=JsonValueKind.False ||
                r.GetProperty("Currency").GetString()!="USDT" || r.GetProperty("Model").GetString()!="STATIC_BOOK_ROUND_TRIP") return Invalid();
            var symbol=r.GetProperty("Symbol").GetString();
            if(symbol is null || !System.Text.RegularExpressions.Regex.IsMatch(symbol,@"\A[A-Z0-9]{2,16}USDT\z")) return Invalid();
            var received=r.GetProperty("ReceivedUtc").GetDateTimeOffset();
            if(r.GetProperty("UpdateId").GetInt64()<=0 || received>evidence.ObservedUtc ||
                evidence.ObservedUtc-received>TimeSpan.FromSeconds(30)) return Invalid();
            var fee=r.GetProperty("FeePercent").GetDecimal();
            var impact=r.GetProperty("MaxImpactPercent").GetDecimal();
            var cost=r.GetProperty("MaxCostPercent").GetDecimal();
            if(fee<0 || fee>=100 || impact<0 || impact>100 || cost<0 || cost>100) return Invalid();
            var rows=r.GetProperty("Scenarios");
            if(rows.GetArrayLength() is <1 or >5) return Invalid();
            var findings=new List<string>();
            var quantities=new HashSet<decimal>();
            foreach(var row in rows.EnumerateArray())
            {
                if(!Unique(row)) return Invalid();
                var q=row.GetProperty("Quantity").GetDecimal();
                var entry=row.GetProperty("EntryFilled").GetDecimal();
                var exit=row.GetProperty("ExitFilled").GetDecimal();
                if(q<=0 || !quantities.Add(q) || entry<0 || exit<0 || entry>q || exit>q) return Invalid();
                string expected;
                if(entry<q || exit<q)
                {
                    if(new[]{"TotalEntry","NetExit","NetResult","NetReturnPercent","BreakEvenExitAverage"}.Any(k=>row.GetProperty(k).ValueKind!=JsonValueKind.Null)) return Invalid();
                    expected="INSUFFICIENT_DEPTH";
                }
                else
                {
                    var total=row.GetProperty("TotalEntry").GetDecimal();
                    var proceeds=row.GetProperty("NetExit").GetDecimal();
                    var net=row.GetProperty("NetResult").GetDecimal();
                    var ret=row.GetProperty("NetReturnPercent").GetDecimal();
                    var ei=row.GetProperty("EntryImpact").GetDecimal();
                    var xi=row.GetProperty("ExitImpact").GetDecimal();
                    if(total<=0 || proceeds<=0 || ei<0 || xi<0 || net!=proceeds-total || ret!=net/total*100m ||
                        row.GetProperty("BreakEvenExitAverage").GetDecimal()!=total/(q*(1-fee/100m))) return Invalid();
                    expected=ei>impact || xi>impact?"EXCESSIVE_IMPACT":-ret>cost?"EXCESSIVE_COST":"RESEARCH_ONLY";
                }
                if(row.GetProperty("Status").GetString()!=expected) return Invalid();
                if(expected!="RESEARCH_ONLY") findings.Add(expected);
            }
            return new(evidence.MessageId,symbol,findings.AsReadOnly(),findings.Count>0?"REVIEW_REQUIRED":"HUMAN_REVIEW_REQUIRED");
        }
        catch(Exception ex) when(ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException or ArithmeticException)
        {return Invalid();}
    }
    private static bool Unique(JsonElement r)=>r.ValueKind==JsonValueKind.Object &&
        r.EnumerateObject().Select(x=>x.Name).Distinct(StringComparer.Ordinal).Count()==r.EnumerateObject().Count();
}
