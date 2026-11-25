using Microsoft.EntityFrameworkCore;
using RAC.Application;
namespace RAC.Infrastructure;

// Explicit persistence only; does not activate workers or authorize trading.
public sealed class RacAnaReviewRequestStore(IDbContextFactory<RacDbContext> factory)
{
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
            if(missing.Length==0 && task is not null) return 0;
            db.AgentMessages.AddRange(missing);
            try { await db.SaveChangesAsync(ct); return missing.Length; }
            catch(DbUpdateException ex) when(attempt<3 && ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 2601 or 2627) { }
        }
    }
}
