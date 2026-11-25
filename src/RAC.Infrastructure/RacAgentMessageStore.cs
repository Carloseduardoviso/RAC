using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;

namespace RAC.Infrastructure;

public sealed class RacAgentMessageStore(IDbContextFactory<RacDbContext> factory)
{
    public static RacAgentMessageRecord Validate(RacAgentMessage message)
    {
        if (message.Id == Guid.Empty) throw new ArgumentException("Missing message ID.");
        RacAgentCommunication.Create(message.Sender, message.Recipient, message.Kind,
            message.Content, message.CorrelationId, message.CreatedUtc);
        return new RacAgentMessageRecord
        {
            Id = message.Id, CorrelationId = message.CorrelationId,
            Sender = message.Sender, Recipient = message.Recipient,
            Kind = message.Kind.ToString(), Content = message.Content,
            CreatedUtc = message.CreatedUtc.ToUniversalTime()
        };
    }

    public async Task<bool> SaveAsync(RacAgentMessage message, CancellationToken ct)
    {
        var row = Validate(message);
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var existing = await db.AgentMessages.SingleOrDefaultAsync(x => x.Id == row.Id, ct);
        if (existing is not null)
        {
            if (existing.CorrelationId != row.CorrelationId || existing.Sender != row.Sender ||
                existing.Recipient != row.Recipient || existing.Kind != row.Kind ||
                existing.Content != row.Content)
                throw new InvalidOperationException("Message ID collision.");
            return false;
        }
        db.AgentMessages.Add(row);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    public async Task<RacAgentMessage> SaveFinancialAdvisoryAsync(RacAgentMessage message,CancellationToken ct)
    {
        var row=Validate(message);
        if(message.Sender!="carlos" || message.Recipient!="ana" ||
            message.Kind is not (RacMessageKind.Report or RacMessageKind.RiskNotice))
            throw new ArgumentException("Invalid financial advisory route.");
        await using var db=await factory.CreateDbContextAsync(ct);
        db.AgentMessages.Add(row);
        try { await db.SaveChangesAsync(ct); return message; }
        catch(DbUpdateException ex) when(ex.InnerException is Microsoft.Data.SqlClient.SqlException sql &&
            sql.Number is 2601 or 2627)
        {
            db.ChangeTracker.Clear();
            var existing=await db.AgentMessages.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==row.Id,ct);
            if(existing is null) throw;
            if(existing.CorrelationId!=row.CorrelationId || existing.Sender!=row.Sender ||
                existing.Recipient!=row.Recipient || existing.Kind!=row.Kind || existing.Content!=row.Content)
                throw new InvalidOperationException("Advisory identity collision.",ex);
            return message with {CreatedUtc=existing.CreatedUtc};
        }
    }

    public async Task<IReadOnlyList<RacAgentMessageRecord>> ReadInboxAsync(string recipient,
        Guid? correlationId, int limit, CancellationToken ct)
    {
        if (!RacOrganization.All.Any(x => x.Id == recipient) || limit is < 1 or > 500 ||
            correlationId == Guid.Empty) throw new ArgumentException("Invalid inbox query.");
        await using var db = await factory.CreateDbContextAsync(ct);
        var query = db.AgentMessages.AsNoTracking().Where(x => x.Recipient == recipient);
        if (correlationId.HasValue) query = query.Where(x => x.CorrelationId == correlationId.Value);
        return await query.OrderByDescending(x => x.CreatedUtc).ThenBy(x => x.Id).Take(limit).ToListAsync(ct);
    }
}
