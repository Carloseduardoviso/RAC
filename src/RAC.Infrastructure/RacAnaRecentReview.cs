using Microsoft.EntityFrameworkCore;
using RAC.Application;

namespace RAC.Infrastructure;

// Single read-only query; reject overflow instead of silently dropping risk notices.
public sealed class RacAnaRecentReview(IDbContextFactory<RacDbContext> factory, IndependentResearchFreezeStore? research=null)
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
        if(rows.Count == 0) return await ReviewResearchAsync(RacAnaAdvisory.Summarize([]),ct);
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
        var marketAuditor=new RacAnaMarketEvidenceAudit(factory);
        foreach(var report in batch.SelectMany(x=>x.MarketReports))
        {
            var verified=await marketAuditor.VerifyAsync(byId[report.MessageId],report,maxAge,ct);
            findings.Add($"{report.MessageId}:MARKET_PROVENANCE:{(verified?"VERIFIED":"UNVERIFIED")}");
            invalid|=!verified;
        }
        return await ReviewResearchAsync(result with {Status=invalid?"RISK_REVIEW_REQUIRED":result.Status,
            Reasons=Array.AsReadOnly(result.Reasons.Concat(findings).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray()),
            EvidenceIds=Array.AsReadOnly(result.EvidenceIds.Concat(originals).Distinct().OrderBy(x=>x).ToArray())},ct);
    }
    private async Task<RacAnaAdvisoryResult> ReviewResearchAsync(RacAnaAdvisoryResult result,CancellationToken ct)
    {
        if(research is null)return result;
        await using var db=await factory.CreateDbContextAsync(ct);
        var records=await db.IndependentResearchFreezes.AsNoTracking().OrderBy(x=>x.ConfigurationId).Take(481).ToListAsync(ct);
        var findings=new List<string>();
        var invalid=records.Count>480;
        if(invalid)findings.Add("RESEARCH:CAPACITY_EXCEEDED");
        else if(records.Count==0)findings.Add("RESEARCH:NO_FROZEN_EVIDENCE");
        else
        {
            var verified=new HashSet<string>(StringComparer.Ordinal);
            foreach(var record in records)
            {
                try
                {
                    var p=System.Text.Json.JsonSerializer.Deserialize<ResearchBacktestParameters.Experiment>(record.ParametersJson)
                        ?? throw new InvalidOperationException("Missing parameters.");
                    var row=new ResearchExperimentPlan.Row(record.Symbol,p);
                    if(ResearchExperimentIdentity.Create(row)!=record.ConfigurationId)throw new InvalidOperationException("Invalid configuration.");
                    if(p.SlippagePctPerSide!=.20m)continue;
                    foreach(var id in await research.AuditPairAsync(row,record.ManifestId,ct))verified.Add(id);
                var reader=new IndependentResearchReviewReader(factory,research,TimeProvider.System);
                var assessment=await reader.ReadAsync(row,record.ManifestId,ct);
                findings.Add($"RESEARCH:CLASSIFICATION:{assessment.Classification.Status}");
                findings.Add($"RESEARCH:DETERIORATION:{assessment.Deterioration.Status}");
                findings.AddRange(assessment.Recommendations.Actions.Select(x=>$"RESEARCH:ACTION:{x}"));
                invalid|=assessment.Classification.Status=="INVALID_EVIDENCE" || assessment.Deterioration.Status is "INVALID_EVIDENCE" or "DETERIORATION_REVIEW_REQUIRED";
                }
                catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
                {
                    invalid=true;
                }
            }
            invalid|=records.Any(x=>!verified.Contains(x.FreezeId));
            findings.Add($"RESEARCH:VERIFIED_FREEZES:{verified.Count}:{records.Count}");
            var digest=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("|",verified.OrderBy(x=>x,StringComparer.Ordinal)))));
        findings.Add($"RESEARCH:FREEZE_SET:{digest}");
        }
        findings.Add("RESEARCH:INDEPENDENT_VALIDATION_REQUIRED");
        return result with {Status=invalid?"RISK_REVIEW_REQUIRED":result.Status,
            Reasons=Array.AsReadOnly(result.Reasons.Concat(findings).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray())};
    }
}
