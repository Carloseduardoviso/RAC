using System.Text.Json;
namespace RAC.Application;

public sealed record RacAnaMarketReportResult(Guid MessageId,string Status,
    IReadOnlyList<Guid> EvidenceIds,IReadOnlyList<string> Reasons);

// Parses only the versioned Rebeca market envelope. Unknown/malformed envelopes fail closed.
public static class RacAnaMarketReportReview
{
    public static RacAnaMarketReportResult Review(RacAgentMessage message)
    {
        if(message.Id==Guid.Empty || message.Sender!="rebeca" || message.Recipient!="ana" ||
            message.Kind!=RacMessageKind.Report || string.IsNullOrWhiteSpace(message.Content) ||
            message.Content.Length>4096 || !message.Content.TrimStart().StartsWith('{'))
            return Invalid(message.Id,"INVALID_ROUTE_OR_ENVELOPE");
        try
        {
            using var document=JsonDocument.Parse(message.Content);
            var root=document.RootElement;
            if(root.ValueKind!=JsonValueKind.Object ||
                !String(root,"Schema","REBECA_MARKET_V1") ||
                !String(root,"Mode","RESEARCH_ONLY") ||
                !Boolean(root,"TradingAuthorized",false) ||
                !Boolean(root,"HumanReviewRequired",true) ||
                !root.TryGetProperty("Status",out var statusElement) || statusElement.ValueKind!=JsonValueKind.String ||
                !root.TryGetProperty("EvidenceIds",out var idsElement) || idsElement.ValueKind!=JsonValueKind.Array ||
                !root.TryGetProperty("Reasons",out var reasonsElement) || reasonsElement.ValueKind!=JsonValueKind.Array)
                return Invalid(message.Id,"INVALID_SCHEMA_OR_PERMISSION_FLAGS");
            var status=statusElement.GetString()!;
            if(status is not ("CRITICAL_REVIEW" or "INCOMPLETE_EVIDENCE" or "HUMAN_REVIEW_REQUIRED"))
                return Invalid(message.Id,"INVALID_STATUS");
            var ids=new List<Guid>();
            foreach(var item in idsElement.EnumerateArray())
                if(item.ValueKind!=JsonValueKind.String || !Guid.TryParse(item.GetString(),out var id) || id==Guid.Empty)
                    return Invalid(message.Id,"INVALID_EVIDENCE_IDS");
                else ids.Add(id);
            var reasons=new List<string>();
            foreach(var item in reasonsElement.EnumerateArray())
                if(item.ValueKind!=JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString()))
                    return Invalid(message.Id,"INVALID_REASONS");
                else reasons.Add(item.GetString()!);
            if(reasons.Count==0 || (ids.Count==0 && status!="INCOMPLETE_EVIDENCE"))
                return Invalid(message.Id,"INCOMPLETE_MARKET_ENVELOPE");
            return new(message.Id,status,Array.AsReadOnly(ids.Distinct().OrderBy(x=>x).ToArray()),
                Array.AsReadOnly(reasons.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray()));
        }
        catch(JsonException) { return Invalid(message.Id,"MALFORMED_MARKET_JSON"); }
        catch(InvalidOperationException) { return Invalid(message.Id,"MALFORMED_MARKET_JSON"); }
    }
    private static bool String(JsonElement root,string name,string expected) =>
        root.TryGetProperty(name,out var value) && value.ValueKind==JsonValueKind.String && value.GetString()==expected;
    private static bool Boolean(JsonElement root,string name,bool expected) =>
        root.TryGetProperty(name,out var value) && (value.ValueKind==JsonValueKind.True || value.ValueKind==JsonValueKind.False) && value.GetBoolean()==expected;
    private static RacAnaMarketReportResult Invalid(Guid id,string reason)=>new(id,"INVALID_REPORT",[],[reason]);
}
