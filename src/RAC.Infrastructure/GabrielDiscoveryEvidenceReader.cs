namespace RAC.Infrastructure;

public sealed class GabrielDiscoveryEvidenceReader(RebecaMarketEvidenceReader reader)
{
    public async Task<RebecaDanielReportResult> ReadAsync(Guid taskId,DateTimeOffset now,CancellationToken ct)
        => Validate(await reader.ReadAsync("daniel",taskId,now,TimeSpan.FromMinutes(15),ct),taskId,now);

    public static RebecaDanielReportResult Validate(IReadOnlyList<RebecaMarketEvidence> rows,Guid taskId,DateTimeOffset now)
    {
        if(taskId==Guid.Empty || rows.Count!=1) throw new InvalidOperationException("Unique discovery evidence required.");
        var row=rows[0];
        if(row.TaskId!=taskId || row.ObservedUtc>now || now-row.ObservedUtc>TimeSpan.FromMinutes(15))
            throw new InvalidOperationException("Invalid discovery provenance or age.");
        var result=RebecaDanielReportReview.Review(row);
        if(result.Status!="RESEARCH_ONLY" || result.CoverageStatus!="RESEARCH_CANDIDATES_AVAILABLE")
            throw new InvalidOperationException("Complete discovery evidence required.");
        return result;
    }
}
