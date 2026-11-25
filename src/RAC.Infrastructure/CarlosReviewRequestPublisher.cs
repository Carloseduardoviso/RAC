using Microsoft.EntityFrameworkCore;
using RAC.Application;
namespace RAC.Infrastructure;

// Explicit invocation only; waits for all three persisted tasks.
public sealed class CarlosReviewRequestPublisher(IDbContextFactory<RacDbContext> factory,CarlosFinancialAdvisoryPublisher publisher)
{
    public async Task<RacAgentMessage?> PublishAsync(Guid requestId,DateTimeOffset now,TimeSpan maximumAge,CancellationToken ct)
    {
        if(requestId==Guid.Empty || maximumAge<=TimeSpan.Zero || maximumAge>TimeSpan.FromDays(1)) throw new ArgumentException("Invalid review request.");
        await using var db=await factory.CreateDbContextAsync(ct);
        var request=await db.AgentMessages.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==requestId,ct);
        if(request is null || request.Sender!="ana" || request.Recipient!="carlos" || request.Kind!="TaskRequest") return null;
        using var json=System.Text.Json.JsonDocument.Parse(request.Content);
        var root=json.RootElement;
        if(root.GetProperty("Schema").GetString()!="ANA_REVIEW_V1" || root.GetProperty("TradingAuthorized").GetBoolean() || !root.GetProperty("HumanReviewRequired").GetBoolean()) return null;
        var plan=RacCarlosReviewTasks.Create(requestId,now);
        var ids=plan.Select(x=>x.Id).ToArray();
        var rows=await db.AgentTasks.AsNoTracking().Where(x=>ids.Contains(x.Id)).ToListAsync(ct);
        if(plan.Any(x=>!rows.Any(r=>r.Id==x.Id && r.Leader==x.Leader && r.Specialist==x.Specialist && r.Description==x.Description && r.Status=="Completed"))) return null;
        return await publisher.PublishAsync(request.CorrelationId,plan.Single(x=>x.Specialist=="rafael").Id,plan.Single(x=>x.Specialist=="samuel").Id,plan.Single(x=>x.Specialist=="miguel").Id,now,maximumAge,ct);
    }
}
