using Microsoft.EntityFrameworkCore;
using RAC.Application;
using System.Data;
namespace RAC.Infrastructure;
public sealed record MiguelStoredAudit(MiguelConsolidatedAudit Audit,bool RiskLocked,bool Running,
    DateTimeOffset UpdatedUtc,DateTimeOffset? LastCycleUtc,int FillCount,int PositionCount)
{
    public bool TradingAuthorized=>false;
}
public sealed class MiguelAuditReader(IDbContextFactory<RacDbContext> factory)
{
    public async Task<MiguelStoredAudit?> ReadAsync(decimal moneyTolerance,decimal quantityTolerance,CancellationToken ct)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var account=await db.PaperAccounts.AsNoTracking().SingleOrDefaultAsync(ct);
        if(account is null) return null;
        var fills=await db.PaperFills.AsNoTracking().OrderBy(x=>x.Id).Take(100001)
            .Select(x=>new MiguelFill(x.Id,x.Symbol,x.Side,x.Quantity,x.PriceBrl,x.FeeBrl,x.RealizedPnlBrl,x.ExecutedUtc)).ToListAsync(ct);
        var positions=await db.PaperPositions.AsNoTracking().OrderBy(x=>x.Id).Take(10001)
            .Select(x=>new MiguelOpenPosition(x.Symbol,x.Quantity,x.EntryPriceBrl,x.EntryFeeBrl,x.LastPriceBrl)).ToListAsync(ct);
        if(fills.Count>100000 || positions.Count>10000) throw new InvalidOperationException("Audit history exceeds supported bounds.");
        await tx.CommitAsync(ct);
        var audit=MiguelIntegrityAudit.Evaluate(account.CashBrl,account.TotalContributionsBrl,
            account.LastEquityBrl,fills,positions,moneyTolerance,quantityTolerance);
        return new(audit,account.RiskLocked,account.Running,account.UpdatedUtc,account.LastCycleUtc,fills.Count,positions.Count);
    }
}
