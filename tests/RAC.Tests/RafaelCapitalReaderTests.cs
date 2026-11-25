using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class RafaelCapitalReaderTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task ReadsStoredPaperCapitalWithoutChangingIt()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_RAFAEL_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using var db=new RacDbContext(options);
            await db.Database.MigrateAsync();
            var reader=new RafaelCapitalReader(new Factory(options));
            Assert.Null(await reader.ReadAsync(default));
            db.PaperAccounts.Add(new PaperAccount{CashBrl=80,TotalContributionsBrl=100,RiskLocked=true});
            db.PaperPositions.Add(new PaperPosition{Symbol="BTCUSDT",Quantity=2,LastPriceBrl=10,EntryPriceBrl=9});
            await db.SaveChangesAsync();
            var result=Assert.IsType<RafaelStoredCapital>(await reader.ReadAsync(default));
            Assert.Equal(80m,result.Snapshot.Cash);
            Assert.Equal(20m,result.Snapshot.MarkedPositions);
            Assert.True(result.Snapshot.RiskLocked);
            Assert.False(result.TradingAuthorized);
            Assert.Equal(1,result.PositionCount);
            Assert.Equal(0,result.FillCount);
            Assert.Equal(80m,(await db.PaperAccounts.AsNoTracking().SingleAsync()).CashBrl);
            Assert.True((await db.PaperAccounts.AsNoTracking().SingleAsync()).RiskLocked);
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
