using Microsoft.EntityFrameworkCore;
using RAC.Application;

namespace RAC.Infrastructure;

// Single read-only query; reject overflow instead of silently dropping risk notices.
public sealed class RacAnaRecentReview(IDbContextFactory<RacDbContext> factory)
{
    public async Task<RacAnaAdvisoryResult> ReviewAsync(DateTimeOffset now,
        TimeSpan maxAge, CancellationToken ct)
    {
        if(maxAge <= TimeSpan.Zero || maxAge > TimeSpan.FromDays(1))
            throw new ArgumentException("Invalid review window.");
        var since = now - maxAge;
        await using var db = await factory.CreateDbContextAsync(ct);
        var rows = await db.AgentMessages.AsNoTracking()
            .Where(x => x.Recipient == "ana" && x.CreatedUtc >= since && x.CreatedUtc <= now &&
                (x.Kind == "Report" || x.Kind == "RiskNotice") &&
                (x.Sender == "rebeca" || x.Sender == "carlos" || x.Sender == "lucas"))
            .OrderBy(x => x.CreatedUtc).ThenBy(x => x.Id).Take(501).ToListAsync(ct);
        if(rows.Count > 500) throw new InvalidOperationException("Review evidence overflow.");
        if(rows.Count == 0) return RacAnaAdvisory.Summarize([]);
        var evidence = rows.Select(x => new RacAgentMessage(x.Id,x.CorrelationId,x.Sender,x.Recipient,
            x.Kind == "Report" ? RacMessageKind.Report : RacMessageKind.RiskNotice,x.Content,x.CreatedUtc));
        var batch=RacAnaEvidenceBatch.Review(rows.Select(x=>x.CorrelationId),evidence,now,maxAge);
        var result=RacAnaAdvisory.Summarize(batch);
        var auditor=new RacAnaFinancialEvidenceAudit(factory);
        var originals=new List<Guid>();
        var findings=new List<string>();
        bool invalid=false;
        var financial=batch.SelectMany(x=>x.FinancialReports).Where(x=>x.Status!="INVALID_FINANCIAL_REPORT").ToArray();
        var byId=evidence.ToDictionary(x=>x.Id);
        var audits=(await auditor.ReviewBatchAsync(financial.Where(x=>!x.Reasons.Contains("CAPACITY_EXCEEDED",StringComparer.Ordinal)).Select(x=>byId[x.MessageId]),maxAge,ct)).ToDictionary(x=>x.MessageId);
        foreach(var report in financial)
        {
            if(report.Reasons.Contains("CAPACITY_EXCEEDED",StringComparer.Ordinal))
            {
                var store=new RacAgentMessageStore(factory);
                var recovery=new RacAnaFinancialCapacityRecovery(new CarlosFinancialEvidenceService(
                    new CarlosRafaelEvidenceReader(store),new CarlosSamuelEvidenceReader(store),new CarlosMiguelEvidenceReader(store)));
                try
                {
                    var recovered=await recovery.ReviewAsync(byId[report.MessageId],maxAge,ct);
                    originals.AddRange(recovered.EvidenceIds);
                    findings.Add($"{report.MessageId}:CAPACITY_RECOVERY:{(recovered.Verified?"VERIFIED":"MISMATCH")}");
                    invalid|=!recovered.Verified;
                }
                catch(InvalidOperationException)
                {
                    findings.Add($"{report.MessageId}:CAPACITY_RECOVERY:UNAVAILABLE");
                    invalid=true;
                }
                continue;
            }
            var audit=audits[report.MessageId];
            originals.AddRange(audit.VerifiedIds);
            findings.Add($"{report.MessageId}:FINANCIAL_PROVENANCE:{audit.VerifiedIds.Count}:{audit.InvalidIds.Count}");
            foreach(var id in audit.InvalidIds) findings.Add($"{report.MessageId}:INVALID_EVIDENCE:{id}");
            invalid|=audit.InvalidIds.Count>0;
        }
        return result with {Status=invalid?"RISK_REVIEW_REQUIRED":result.Status,
            Reasons=Array.AsReadOnly(result.Reasons.Concat(findings).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray()),
            EvidenceIds=Array.AsReadOnly(result.EvidenceIds.Concat(originals).Distinct().OrderBy(x=>x).ToArray())};
    }
}
