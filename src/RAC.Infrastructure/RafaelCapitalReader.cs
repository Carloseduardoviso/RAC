using Microsoft.EntityFrameworkCore;
using RAC.Application;
using System.Data;
namespace RAC.Infrastructure;

public sealed record RafaelStoredCapital(RafaelCapitalSnapshot Snapshot,DateTimeOffset UpdatedUtc,
    DateTimeOffset? LastCycleUtc,bool Running,int PositionCount,int FillCount,decimal RealizedNetProfit)
{
    public bool TradingAuthorized=>false;
}
public sealed class RafaelCapitalReader(IDbContextFactory<RacDbContext> factory)
{
    public async Task<RafaelStoredCapital?> ReadAsync(CancellationToken ct)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(IsolationLevel.Serializable,ct);
        var account=await db.PaperAccounts.AsNoTracking().SingleOrDefaultAsync(ct);
        if(account is null) return null;
        var positions=await db.PaperPositions.AsNoTracking().ToListAsync(ct);
        if(positions.Any(x=>x.Quantity<=0 || x.LastPriceBrl<=0))
            throw new InvalidOperationException("Invalid stored position valuation.");
        var marked=positions.Sum(x=>x.Quantity*x.LastPriceBrl);
        var fills=await db.PaperFills.AsNoTracking().CountAsync(ct);
        var realized=await db.PaperFills.AsNoTracking().SumAsync(x=>x.RealizedPnlBrl??0m,ct);
        var snapshot=new RafaelCapitalSnapshot(account.CashBrl,marked,account.TotalContributionsBrl,account.RiskLocked);
        RafaelCapitalAnalysis.Evaluate(snapshot);
        await tx.CommitAsync(ct);
        return new(snapshot,account.UpdatedUtc,account.LastCycleUtc,account.Running,positions.Count,fills,realized);
    }
}
