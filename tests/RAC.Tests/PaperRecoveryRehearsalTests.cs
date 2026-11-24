using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Infrastructure;
namespace RAC.Tests;
public class PaperRecoveryRehearsalTests
{
    [Fact] public async Task AtomicCloseAndDuplicateRejected()
    {
        if(!OperatingSystem.IsWindows())return;
        var name="RAC_RECOVERY_TEST_"+Guid.NewGuid().ToString("N");
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database="+name+";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            long id;
            await using(var db=new RacDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                db.PaperAccounts.Add(new PaperAccount{CashBrl=80,Running=false,RiskLocked=true,RiskReason="review"});
                var pos=new PaperPosition{Symbol="DOTUSDT",Quantity=5,EntryPriceBrl=4,EntryFeeBrl=.02m,StopPriceBrl=3.9m,LastPriceBrl=4,SignalClosedUtc=DateTimeOffset.UtcNow.AddMinutes(-10)};
                db.PaperPositions.Add(pos);await db.SaveChangesAsync();id=pos.Id;
            }
            await using(var db=new RacDbContext(options))
                await PaperRecoveryRehearsal.ApplyAsync(db,id,.6m,6,DateTimeOffset.UtcNow,"isolated-test");
            await using(var db=new RacDbContext(options))
                await Assert.ThrowsAsync<InvalidOperationException>(()=>PaperRecoveryRehearsal.ApplyAsync(db,id,.6m,6,DateTimeOffset.UtcNow,"isolated-test"));
            await using(var db=new RacDbContext(options))
            {
                var a=await db.PaperAccounts.SingleAsync();
                Assert.True(a.RiskLocked);Assert.False(a.Running);Assert.Equal("review",a.RiskReason);
                Assert.Equal(97.973009m,a.CashBrl);Assert.Equal(a.CashBrl,a.LastEquityBrl);
                Assert.Empty(await db.PaperPositions.ToListAsync());
                Assert.Single(await db.PaperFills.ToListAsync());Assert.Single(await db.AuditEvents.ToListAsync());
                Assert.Empty(await db.TestnetOrders.ToListAsync());
            }
        }
        finally{await using var db=new RacDbContext(options);await db.Database.EnsureDeletedAsync();}
    }
}
