namespace RAC.Application;
public sealed record RebecaNewsEvidenceResult(string Symbol,string Status,IReadOnlyList<RacNewsEvent> Events)
{
    public bool TradingAuthorized=>false;
}
public static class RebecaNewsEvidence
{
    public static RebecaNewsEvidenceResult Review(IRacNewsCalendar calendar,string symbol,DateTimeOffset now)
    {
        if(string.IsNullOrWhiteSpace(symbol)) throw new ArgumentException("Missing symbol.");
        var events=calendar.EventsAround(symbol,now);
        if(events is null) return new(symbol,"NEWS_UNAVAILABLE",[]);
        var nearby=events.Where(x=>(x.TimeUtc-now).Duration()<=NewsValidator.Window)
            .Distinct().OrderBy(x=>x.TimeUtc).ThenBy(x=>x.Title,StringComparer.Ordinal)
            .ThenBy(x=>x.Impact,StringComparer.Ordinal).ToArray();
        var conflict=nearby.GroupBy(x=>(x.Title,x.TimeUtc)).Any(g=>g.Select(x=>x.Impact).Distinct().Count()>1);
        var status=nearby.Any(x=>x.Impact=="high")?"HIGH_IMPACT_REVIEW":
            conflict?"NEWS_CONFLICT":nearby.Length==0?"NO_NEARBY_EVENTS":"NEWS_REVIEW_REQUIRED";
        return new(symbol,status,Array.AsReadOnly(nearby));
    }
}
