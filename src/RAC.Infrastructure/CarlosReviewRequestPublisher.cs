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
        if(!IsValidRequest(request.Content)) return null;
        var plan=RacCarlosReviewTasks.Create(requestId,now);
        var ids=plan.Select(x=>x.Id).ToArray();
        var rows=await db.AgentTasks.AsNoTracking().Where(x=>ids.Contains(x.Id)).ToListAsync(ct);
        if(plan.Any(x=>!rows.Any(r=>r.Id==x.Id && r.Leader==x.Leader && r.Specialist==x.Specialist && r.Description==x.Description && r.Status=="Completed"))) return null;
        return await publisher.PublishAsync(request.CorrelationId,plan.Single(x=>x.Specialist=="rafael").Id,plan.Single(x=>x.Specialist=="samuel").Id,plan.Single(x=>x.Specialist=="miguel").Id,now,maximumAge,ct);
    }
    private static bool IsValidRequest(string content)
    {
        try
        {
            using var json=System.Text.Json.JsonDocument.Parse(content);
            var root=json.RootElement;
            return root.ValueKind==System.Text.Json.JsonValueKind.Object &&
                root.TryGetProperty("Schema",out var schema) && schema.ValueKind==System.Text.Json.JsonValueKind.String && schema.GetString()=="ANA_REVIEW_V1" &&
                root.TryGetProperty("TradingAuthorized",out var trading) && trading.ValueKind==System.Text.Json.JsonValueKind.False &&
                root.TryGetProperty("HumanReviewRequired",out var human) && human.ValueKind==System.Text.Json.JsonValueKind.True;
        }
        catch(System.Text.Json.JsonException) { return false; }
    }
}
