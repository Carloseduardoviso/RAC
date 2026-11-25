using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;

namespace RAC.Infrastructure;

public sealed class RacAgentTaskStore(IDbContextFactory<RacDbContext> factory)
{
    private static RacAgentTask Map(RacAgentTaskRecord r) => new(r.Id,r.Leader,r.Specialist,
        r.Description,Enum.Parse<RacTaskStatus>(r.Status),r.Attempts,r.UpdatedUtc,r.LeaseId,r.LeaseUntilUtc);

    private static void Apply(RacAgentTaskRecord r,RacAgentTask t)
    {
        r.Status=t.Status.ToString(); r.Attempts=t.Attempts; r.UpdatedUtc=t.UpdatedUtc;
        r.LeaseId=t.LeaseId; r.LeaseUntilUtc=t.LeaseUntilUtc;
    }

    public async Task<RacAgentTask> EnqueueAsync(string leader,string specialist,string description,CancellationToken ct)
    {
        var t=RacTaskLifecycle.Create(leader,specialist,description,DateTimeOffset.UtcNow);
        var r=new RacAgentTaskRecord { Id=t.Id,Leader=t.Leader,Specialist=t.Specialist,Description=t.Description };
        Apply(r,t);
        await using var db=await factory.CreateDbContextAsync(ct);
        db.AgentTasks.Add(r);
        await db.SaveChangesAsync(ct);
        return t;
    }

    public async Task<RacAgentTask?> ClaimAsync(string specialist,CancellationToken ct)
    {
        if (!RacOrganization.Specialists.Any(x=>x.Id==specialist)) throw new ArgumentException("Unknown specialist.");
        await using var db=await factory.CreateDbContextAsync(ct);
        var r=await db.AgentTasks.Where(x=>x.Specialist==specialist && x.Status=="Pending")
            .OrderBy(x=>x.UpdatedUtc).ThenBy(x=>x.Id).FirstOrDefaultAsync(ct);
        if(r is null) return null;
        var t=RacTaskLifecycle.Start(Map(r),specialist,DateTimeOffset.UtcNow);
        Apply(r,t);
        try { await db.SaveChangesAsync(ct); }
        catch(DbUpdateConcurrencyException) { return null; }
        return t;
    }

    public async Task<RacAgentTask> FinishAsync(Guid id,string specialist,Guid lease,bool success,CancellationToken ct)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        var r=await db.AgentTasks.SingleAsync(x=>x.Id==id,ct);
        var t=RacTaskLifecycle.Finish(Map(r),specialist,lease,success,DateTimeOffset.UtcNow);
        Apply(r,t);
        await db.SaveChangesAsync(ct);
        return t;
    }

    public async Task CompleteWithReportAsync(Guid id,string specialist,Guid lease,RacAgentMessage? message,CancellationToken ct)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var task=await db.AgentTasks.SingleAsync(x=>x.Id==id,ct);
        var finished=RacTaskLifecycle.Finish(Map(task),specialist,lease,true,DateTimeOffset.UtcNow);
        var existing=await db.AgentMessages.Where(x=>x.CorrelationId==id && x.Sender==specialist &&
            x.Recipient==task.Leader && x.Kind=="Report").Take(2).ToListAsync(ct);
        if(existing.Count>1) throw new InvalidOperationException("Multiple task reports.");
        if(message is not null)
        {
            var row=RacAgentMessageStore.Validate(message);
            if(row.CorrelationId!=id || row.Sender!=specialist || row.Recipient!=task.Leader || row.Kind!="Report")
                throw new InvalidOperationException("Report does not match task.");
            if(existing.Count==0) db.AgentMessages.Add(row);
            else if(existing[0].Id!=row.Id || existing[0].Content!=row.Content || existing[0].CreatedUtc!=row.CreatedUtc)
                throw new InvalidOperationException("Conflicting task report.");
        }
        else if(existing.Count!=1) throw new InvalidOperationException("Missing task report.");
        Apply(task,finished);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<int> RecoverExpiredAsync(CancellationToken ct)
    {
        var now=DateTimeOffset.UtcNow;
        await using var db=await factory.CreateDbContextAsync(ct);
        var rows=await db.AgentTasks.Where(x=>x.Status=="Running" && x.LeaseUntilUtc<=now)
            .OrderBy(x=>x.LeaseUntilUtc).Take(100).ToListAsync(ct);
        int count=0;
        foreach(var r in rows)
        {
            Apply(r,RacTaskLifecycle.RecoverExpired(Map(r),now));
            try { await db.SaveChangesAsync(ct); count++; }
            catch(DbUpdateConcurrencyException) { db.Entry(r).State=EntityState.Detached; }
        }
        return count;
    }
}
