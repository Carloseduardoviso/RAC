using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class MiguelAuditReaderSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task ReadsConsistentPaperSnapshotWithoutChangingRiskOrRecords()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_MIGUEL_AUDIT_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using var db=new RacDbContext(options);
            await db.Database.MigrateAsync();
            var reader=new MiguelAuditReader(new Factory(options));
            Assert.Null(await reader.ReadAsync(0.01m,0.000001m,default));
            var now=DateTimeOffset.UtcNow;
            db.PaperAccounts.Add(new PaperAccount {CashBrl=79,TotalContributionsBrl=100,LastEquityBrl=103,RiskLocked=true,Running=false});
            db.PaperFills.Add(new PaperFill {Symbol="BTCUSDT",Side="BUY",Reason="TEST",Quantity=2,PriceBrl=10,FeeBrl=1,ExecutedUtc=now,SignalClosedUtc=now});
            db.PaperPositions.Add(new PaperPosition {Symbol="BTCUSDT",Quantity=2,EntryPriceBrl=10,EntryFeeBrl=1,LastPriceBrl=12,StopPriceBrl=9,OpenedUtc=now,SignalClosedUtc=now});
            await db.SaveChangesAsync();
            var result=await reader.ReadAsync(0.01m,0.000001m,default);
            Assert.NotNull(result);
            Assert.True(result.Audit.Consistent);
            Assert.Equal(3m,result.Audit.Positions.Unrealized);
            Assert.True(result.RiskLocked);
            Assert.False(result.TradingAuthorized);
            db.ChangeTracker.Clear();
            var account=await db.PaperAccounts.SingleAsync();
            Assert.Equal(79m,account.CashBrl);
            Assert.True(account.RiskLocked);
            Assert.False(account.Running);
            Assert.Equal(1,await db.PaperFills.CountAsync());
            Assert.Equal(1,await db.PaperPositions.CountAsync());
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
