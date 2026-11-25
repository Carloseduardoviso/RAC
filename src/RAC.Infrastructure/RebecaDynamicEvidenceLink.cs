using System.Text.Json;
namespace RAC.Infrastructure;

public static class RebecaDynamicEvidenceLink
{
    public static RebecaGabrielReportResult Review(RebecaMarketEvidence gabriel,IReadOnlyList<RebecaMarketEvidence> discovery)
    {
        var result=RebecaGabrielReportReview.Review(gabriel);
        if(result.Status=="INVALID_REPORT") return result;
        RebecaGabrielReportResult Invalid()=>new(gabriel.MessageId,"INVALID_REPORT",0,0,null);
        try
        {
            using var doc=JsonDocument.Parse(gabriel.Content);
            var root=doc.RootElement;
            if(!root.TryGetProperty("Schema",out _)) return result;
            if(discovery.Count!=1) return Invalid();
            var source=discovery[0];
            var reviewed=RebecaDanielReportReview.Review(source);
            if(reviewed.Status!="RESEARCH_ONLY" || reviewed.CoverageStatus!="RESEARCH_CANDIDATES_AVAILABLE" ||
                root.GetProperty("DanielTaskId").GetGuid()!=source.TaskId ||
                root.GetProperty("DanielMessageId").GetGuid()!=source.MessageId) return Invalid();
            var symbols=root.GetProperty("Symbols").Deserialize<string[]>();
            if(symbols is null || !symbols.SequenceEqual(reviewed.Candidates.Select(x=>x.Symbol),StringComparer.Ordinal))
                return Invalid();
            var assessments=root.GetProperty("DiscoveryAssessments").EnumerateArray().ToArray();
            if(assessments.Length!=symbols.Length) return Invalid();
            var total=0;
            for(var i=0;i<assessments.Length;i++)
            {
                var row=assessments[i];
                if(row.ValueKind!=JsonValueKind.Object ||
                    row.EnumerateObject().Select(x=>x.Name).Distinct(StringComparer.Ordinal).Count()!=row.EnumerateObject().Count() ||
                    row.GetProperty("Symbol").GetString()!=symbols[i] ||
                    row.GetProperty("TradingAuthorized").ValueKind!=JsonValueKind.False ||
                    row.GetProperty("StressConfigurations").GetInt32()!=12) return Invalid();
                var further=row.GetProperty("FurtherResearch").GetInt32();
                if(further<0 || further>12 ||
                    row.GetProperty("Status").GetString()!=(further>0?"INDEPENDENT_VALIDATION_REQUIRED":"NO_QUALIFYING_CONFIGURATION")) return Invalid();
                total+=further;
            }
            if(total!=result.FurtherResearch) return Invalid();
            return result;
        }
        catch(Exception ex) when(ex is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException)
        {return Invalid();}
    }
}
