using Microsoft.EntityFrameworkCore;
using RAC.Application;
namespace RAC.Infrastructure;

// Explicit persistence only; does not activate workers or authorize trading.
public sealed class RacAnaReviewRequestStore(IDbContextFactory<RacDbContext> factory)
{
    // Only independent incoming reports may seed automatic requests. Derived replies remain in the full advisory.
    public async Task<int> SaveIndependentAsync(RacAnaAdvisoryResult result,DateTimeOffset now,CancellationToken ct)
    {
        if(result.EvidenceIds.Count==0) return 0;
        await using var db=await factory.CreateDbContextAsync(ct);
        var ids=result.EvidenceIds.Distinct().ToArray();
        var roots=await db.AgentMessages.AsNoTracking().Where(x=>ids.Contains(x.Id) && x.Recipient=="ana" &&
            (x.Kind=="Report" || x.Kind=="RiskNotice") && (x.Sender=="rebeca" || x.Sender=="carlos" || x.Sender=="lucas") &&
            !db.AgentMessages.Any(q=>q.Sender=="ana" && q.Kind=="TaskRequest" && (q.Recipient=="carlos" || q.Recipient=="lucas") && (q.CorrelationId==x.CorrelationId || q.Id==x.CorrelationId)))
            .Select(x=>x.Id).ToListAsync(ct);
        if(roots.Count==0) return 0;
        // Stable per-source identity, independent of changes to derived advisory status or reasons.
        var count=0;
        foreach(var root in roots.OrderBy(x=>x))
            count+=await SaveAsync(new RacAnaAdvisoryResult("HUMAN_REVIEW_REQUIRED",["INDEPENDENT_EVIDENCE_REVIEW"],[root]),now,ct);
        return count;
    }

    public async Task<int> SaveAsync(RacAnaAdvisoryResult result,DateTimeOffset now,CancellationToken ct)
    {
        var requests=RacAnaReviewRequests.Create(result,now).Select(RacAgentMessageStore.Validate).ToArray();
        if(requests.Length==0) return 0;
        for(int attempt=0;;attempt++)
        {
            await using var db=await factory.CreateDbContextAsync(ct);
            var ids=requests.Select(x=>x.Id).ToArray();
            var existing=await db.AgentMessages.AsNoTracking().Where(x=>ids.Contains(x.Id)).ToListAsync(ct);
            foreach(var row in existing)
            {
                var expected=requests.Single(x=>x.Id==row.Id);
                if(row.Sender!=expected.Sender || row.Recipient!=expected.Recipient || row.Kind!=expected.Kind || row.CorrelationId!=expected.CorrelationId || row.Content!=expected.Content)
                    throw new InvalidOperationException("Review request identity collision.");
            }
            var missing=requests.Where(x=>existing.All(y=>y.Id!=x.Id)).ToArray();
            var lucas=requests.Single(x=>x.Recipient=="lucas");
            var task=await db.AgentTasks.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==lucas.Id,ct);
            if(task is not null && (task.Leader!="ana" || task.Specialist!="lucas" || task.Description!="EXCHANGE_HEALTH")) throw new InvalidOperationException("Lucas task identity collision.");
            if(task is null) db.AgentTasks.Add(new RAC.Domain.RacAgentTaskRecord {Id=lucas.Id,Leader="ana",Specialist="lucas",Description="EXCHANGE_HEALTH",Status="Pending",UpdatedUtc=now.ToUniversalTime()});
            var financial=RacCarlosReviewTasks.Create(requests.Single(x=>x.Recipient=="carlos").Id,now);
            var financialIds=financial.Select(x=>x.Id).ToArray();
            var stored=await db.AgentTasks.AsNoTracking().Where(x=>financialIds.Contains(x.Id)).ToListAsync(ct);
            foreach(var planned in financial)
            {
                var row=stored.SingleOrDefault(x=>x.Id==planned.Id);
                if(row is not null && (row.Leader!=planned.Leader || row.Specialist!=planned.Specialist || row.Description!=planned.Description)) throw new InvalidOperationException("Financial task identity collision.");
                if(row is null) db.AgentTasks.Add(new RAC.Domain.RacAgentTaskRecord {Id=planned.Id,Leader=planned.Leader,Specialist=planned.Specialist,Description=planned.Description,Status=planned.Status.ToString(),UpdatedUtc=planned.UpdatedUtc});
            }
            if(missing.Length==0 && task is not null && stored.Count==financial.Count) return 0;
            db.AgentMessages.AddRange(missing);
            try { await db.SaveChangesAsync(ct); return missing.Length; }
            catch(DbUpdateException ex) when(attempt<3 && ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 2601 or 2627) { }
        }
    }
}
