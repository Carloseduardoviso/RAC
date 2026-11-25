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
                existing.Content != row.Content || existing.CreatedUtc != row.CreatedUtc)
                throw new InvalidOperationException("Message ID collision.");
            return false;
        }
        db.AgentMessages.Add(row);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
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
