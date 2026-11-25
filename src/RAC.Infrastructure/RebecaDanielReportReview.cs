using RAC.Application;
using System.Text.Json;
namespace RAC.Infrastructure;
public sealed record RebecaDanielReportResult(Guid MessageId,string Status,IReadOnlyList<DanielRankedCandidate> Candidates)
{
    public bool TradingAuthorized=>false;
    public string CoverageStatus {get;init;}="LEGACY_COVERAGE_UNKNOWN";
}
public static class RebecaDanielReportReview
{
    public static RebecaDanielReportResult Review(RebecaMarketEvidence evidence)
    {
        RebecaDanielReportResult Invalid()=>new(evidence.MessageId,"INVALID_REPORT",[]);
        if(evidence.Specialist!="daniel" || evidence.TaskId==Guid.Empty || evidence.MessageId==Guid.Empty ||
            string.IsNullOrWhiteSpace(evidence.Content) || evidence.Content.Length>4096) return Invalid();
        try
        {
            using var doc=JsonDocument.Parse(evidence.Content);
            var root=doc.RootElement;
            if(!Unique(root) || root.GetProperty("Mode").GetString()!="RESEARCH_ONLY" ||
                root.GetProperty("TradingAuthorized").ValueKind!=JsonValueKind.False ||
                root.GetProperty("TaskId").GetGuid()!=evidence.TaskId ||
                root.GetProperty("ObservedUtc").GetDateTimeOffset()!=evidence.ObservedUtc) return Invalid();
            var rows=root.GetProperty("Candidates").EnumerateArray().ToArray();
            if(rows.Length>20) return Invalid();
            var candidates=new List<DanielRankedCandidate>();
            foreach(var row in rows)
            {
                if(!Unique(row) || row.GetProperty("TradingAuthorized").ValueKind!=JsonValueKind.False) return Invalid();
                var symbol=row.GetProperty("Symbol").GetString();
                var rank=row.GetProperty("Rank").GetInt32();
                var volume=row.GetProperty("QuoteVolume24h").GetDecimal();
                var spread=row.GetProperty("SpreadPercent").GetDecimal();
                if(symbol is null || symbol.Length<=4 || symbol.Length>40 || !symbol.EndsWith("USDT",StringComparison.Ordinal) ||
                    rank!=candidates.Count+1 || volume<1000000m || spread<0 || spread>0.2m) return Invalid();
                candidates.Add(new(rank,symbol,volume,spread));
            }
            if(candidates.Select(x=>x.Symbol).Distinct(StringComparer.Ordinal).Count()!=candidates.Count ||
                !candidates.SequenceEqual(candidates.OrderByDescending(x=>x.QuoteVolume24h).ThenBy(x=>x.SpreadPercent).ThenBy(x=>x.Symbol,StringComparer.Ordinal))) return Invalid();
            var coverage=root.TryGetProperty("CoverageStatus",out var value)?value.GetString():"LEGACY_COVERAGE_UNKNOWN";
            if(coverage is not ("LEGACY_COVERAGE_UNKNOWN" or "PARTIAL_COVERAGE" or "NO_VALID_OBSERVATIONS" or "NO_RESEARCH_CANDIDATES" or "RESEARCH_CANDIDATES_AVAILABLE")) return Invalid();
            if((coverage is "NO_VALID_OBSERVATIONS" or "NO_RESEARCH_CANDIDATES") && candidates.Count!=0 ||
                coverage=="RESEARCH_CANDIDATES_AVAILABLE" && candidates.Count==0) return Invalid();
            return new(evidence.MessageId,candidates.Count==0?"NO_CANDIDATES":"RESEARCH_ONLY",candidates.AsReadOnly()) {CoverageStatus=coverage};
        }
        catch(Exception ex) when(ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {return Invalid();}
    }
    private static bool Unique(JsonElement element)=>element.ValueKind==JsonValueKind.Object &&
        element.EnumerateObject().Select(x=>x.Name).Distinct(StringComparer.Ordinal).Count()==element.EnumerateObject().Count();
}
