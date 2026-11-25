namespace RAC.Infrastructure;
public sealed record CarlosFinancialEvidenceResult(string Status,IReadOnlyList<Guid> EvidenceIds,IReadOnlyList<string> Reasons)
{
    public bool TradingAuthorized=>false;
}
public sealed class CarlosFinancialEvidenceService(CarlosRafaelEvidenceReader rafael,
    CarlosSamuelEvidenceReader samuel,CarlosMiguelEvidenceReader miguel)
{
    public async Task<CarlosFinancialEvidenceResult> ReviewAsync(Guid rafaelTask,Guid samuelTask,Guid miguelTask,
        DateTimeOffset now,TimeSpan maximumAge,CancellationToken ct)
    {
        if(new[]{rafaelTask,samuelTask,miguelTask}.Distinct().Count()!=3 ||
            new[]{rafaelTask,samuelTask,miguelTask}.Contains(Guid.Empty) || maximumAge<=TimeSpan.Zero)
            throw new ArgumentException("Invalid financial evidence selection.");
        var r=await rafael.ReadAsync(rafaelTask,now,maximumAge,ct);
        var s=await samuel.ReadAsync(samuelTask,now,maximumAge,ct);
        var m=await miguel.ReadAsync(miguelTask,now,maximumAge,ct);
        var result=Summarize(r.Select(CarlosRafaelReportReview.Review),s.Select(CarlosSamuelReportReview.Review),
            m.Select(CarlosMiguelReportReview.Review));
        var cross=r.Count==1 && m.Count==1
            ?CarlosFinancialCrossCheck.Review(r[0],m[0],0.01m)
            :new[]{"CROSS_CHECK_UNAVAILABLE"};
        if(cross.Count==0) return result;
        var critical=cross.Any(x=>x.EndsWith("_MISMATCH",StringComparison.Ordinal));
        var status=result.Status=="RISK_LOCKED"?"RISK_LOCKED":
            critical || result.Status=="CRITICAL_REVIEW"?"CRITICAL_REVIEW":"REVIEW_REQUIRED";
        return result with {Status=status,Reasons=Array.AsReadOnly(result.Reasons
            .Concat(cross.Select(x=>"CROSS_CHECK:"+x)).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray())};
    }
    public static CarlosFinancialEvidenceResult Summarize(IEnumerable<CarlosRafaelReportResult> rafael,
        IEnumerable<CarlosSamuelReportResult> samuel,IEnumerable<CarlosMiguelReportResult> miguel)
    {
        var groups=new[]{(Name:"RAFAEL",Rows:rafael.Select(x=>(x.MessageId,x.Status)).ToArray()),
            (Name:"SAMUEL",Rows:samuel.Select(x=>(x.MessageId,x.Status)).ToArray()),
            (Name:"MIGUEL",Rows:miguel.Select(x=>(x.MessageId,x.Status)).ToArray())};
        var reasons=new List<string>();
        var ids=new List<Guid>();
        bool locked=false,critical=false;
        foreach(var group in groups)
        {
            if(group.Rows.Length==0) reasons.Add(group.Name+":MISSING");
            if(group.Rows.Length>1) reasons.Add(group.Name+":MULTIPLE_REPORTS");
            foreach(var row in group.Rows)
            {
                ids.Add(row.MessageId);
                locked|=row.Status=="RISK_LOCKED";
                critical|=row.Status is "CRITICAL_REVIEW" or "INVALID_REPORT";
                if(row.Status!="HUMAN_REVIEW_REQUIRED") reasons.Add(group.Name+":"+row.Status);
            }
        }
        var status=locked?"RISK_LOCKED":critical?"CRITICAL_REVIEW":reasons.Count>0?"REVIEW_REQUIRED":"HUMAN_REVIEW_REQUIRED";
        if(reasons.Count==0) reasons.Add("HUMAN_REVIEW_REQUIRED");
        return new(status,Array.AsReadOnly(ids.Distinct().OrderBy(x=>x).ToArray()),
            Array.AsReadOnly(reasons.Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray()));
    }
}
