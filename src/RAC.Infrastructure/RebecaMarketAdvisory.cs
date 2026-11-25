using RAC.Application;
namespace RAC.Infrastructure;
public sealed record RebecaMarketAdvisoryResult(string Status,IReadOnlyList<Guid> EvidenceIds,IReadOnlyList<string> Reasons,IReadOnlyList<RebecaNewsEvidenceResult> News)
{
    public bool TradingAuthorized=>false;
}
public sealed class RebecaMarketAdvisory(RebecaMarketEvidenceReader reader,IRacNewsCalendar calendar)
{
    public async Task<RebecaMarketAdvisoryResult> ReviewAsync(Guid danielTask,Guid gabrielTask,DateTimeOffset now,TimeSpan maximumAge,CancellationToken ct)
    {
        if(danielTask==gabrielTask || danielTask==Guid.Empty || gabrielTask==Guid.Empty) throw new ArgumentException("Invalid specialist tasks.");
        var d=await reader.ReadAsync("daniel",danielTask,now,maximumAge,ct);
        var g=await reader.ReadAsync("gabriel",gabrielTask,now,maximumAge,ct);
        return Summarize(d.Select(RebecaDanielReportReview.Review).ToArray(),g.Select(RebecaGabrielReportReview.Review).ToArray(),calendar,now);
    }
    public static RebecaMarketAdvisoryResult Summarize(IReadOnlyList<RebecaDanielReportResult> daniel,
        IReadOnlyList<RebecaGabrielReportResult> gabriel,IRacNewsCalendar calendar,DateTimeOffset now)
    {
        var reasons=new List<string>();
        if(daniel.Count!=1) reasons.Add(daniel.Count==0?"DANIEL:MISSING":"DANIEL:MULTIPLE");
        if(gabriel.Count!=1) reasons.Add(gabriel.Count==0?"GABRIEL:MISSING":"GABRIEL:MULTIPLE");
        reasons.AddRange(daniel.Select(x=>"DANIEL:"+x.Status));
        reasons.AddRange(gabriel.Select(x=>"GABRIEL:"+x.Status));
        var news=daniel.Count==1 && daniel[0].Status=="RESEARCH_ONLY"
            ?daniel[0].Candidates.Select(x=>RebecaNewsEvidence.Review(calendar,x.Symbol,now)).ToArray():[];
        reasons.AddRange(news.Select(x=>"NEWS:"+x.Symbol+":"+x.Status));
        var critical=daniel.Any(x=>x.Status=="INVALID_REPORT") || gabriel.Any(x=>x.Status=="INVALID_REPORT") ||
            news.Any(x=>x.Status is "HIGH_IMPACT_REVIEW" or "NEWS_CONFLICT");
        var incomplete=daniel.Count!=1 || gabriel.Count!=1 || news.Any(x=>x.Status=="NEWS_UNAVAILABLE");
        return new(critical?"CRITICAL_REVIEW":incomplete?"INCOMPLETE_EVIDENCE":"HUMAN_REVIEW_REQUIRED",
            Array.AsReadOnly(daniel.Select(x=>x.MessageId).Concat(gabriel.Select(x=>x.MessageId)).Distinct().OrderBy(x=>x).ToArray()),
            Array.AsReadOnly(reasons.Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(news));
    }
}
