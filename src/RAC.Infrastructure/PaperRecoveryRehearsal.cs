using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using System.Data;

namespace RAC.Infrastructure;

// Isolated SQL rehearsal only. Never register as an operational service.
public static class PaperRecoveryRehearsal
{
    public static async Task ApplyAsync(RacDbContext db,long positionId,decimal quote,decimal rate,
        DateTimeOffset observed, string reviewer, CancellationToken ct=default, PaperRecoveryApproval.Evidence? approval=null, bool authorized=false)
    {
        var connection=db.Database.GetDbConnection();
        if(!connection.Database.StartsWith("RAC_RECOVERY_TEST_",StringComparison.Ordinal) ||
           !connection.DataSource.Equals(@"(localdb)\MSSQLLocalDB",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Isolated LocalDB rehearsal required.");
        if(string.IsNullOrWhiteSpace(reviewer)||reviewer.Length>100)throw new InvalidOperationException("Reviewer required.");
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var account=await db.PaperAccounts.SingleAsync(x=>x.Id==1,ct);
        var pos=await db.PaperPositions.SingleAsync(ct);
        if(pos.Id!=positionId || await db.TestnetOrders.AnyAsync(ct))
            throw new InvalidOperationException("Unexpected recovery state.");
        var now=DateTimeOffset.UtcNow;
        var p=PaperStopRecovery.Calculate(account.Running,account.RiskLocked,
            new(pos.Id,pos.Symbol,pos.Quantity,pos.EntryPriceBrl,pos.EntryFeeBrl,pos.StopPriceBrl),
            account.CashBrl,quote,rate,observed,now);
        if(approval is not null)
            await PaperRecoveryApprovalStore.ConsumeAsync(db,approval,reviewer,authorized,pos.Id,pos.Symbol,
                Convert.ToBase64String(account.RowVersion),now,ct);
        account.CashBrl=p.EquityAfter;
        account.LastEquityBrl=p.EquityAfter;
        account.UpdatedUtc=now;
        db.PaperFills.Add(new PaperFill {Symbol=pos.Symbol,Side="SELL",Reason="RECOVERY_REHEARSAL",
            Quantity=pos.Quantity,PriceBrl=p.SellPrice,FeeBrl=p.Fee,RealizedPnlBrl=p.Realized,
            ExecutedUtc=now,SignalClosedUtc=pos.SignalClosedUtc});
        db.PaperPositions.Remove(pos);
        db.AuditEvents.Add(new AuditEvent{EventType="PAPER_RECOVERY_REHEARSAL",
            Description=$"Position {positionId}; reviewer {reviewer}; simulated stop recovery."});
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
