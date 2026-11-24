using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;
public class PaperRecoveryRehearsalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AtomicCloseAndDuplicateRejected(bool concurrent)
    {
        if(!OperatingSystem.IsWindows())return;
        var name="RAC_RECOVERY_TEST_"+Guid.NewGuid().ToString("N");
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database="+name+";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            long id;
            PaperRecoveryApproval.Evidence approval;
            await using(var db=new RacDbContext(options))
            {
                await db.Database.EnsureCreatedAsync();
                await db.Database.ExecuteSqlRawAsync("CREATE TABLE rac.RecoveryApprovalUses (ApprovalId uniqueidentifier PRIMARY KEY, Reviewer nvarchar(100) NOT NULL, PositionId bigint NOT NULL, ConsumedUtc datetimeoffset NOT NULL)");
                db.PaperAccounts.Add(new PaperAccount{CashBrl=80,Running=false,RiskLocked=true,RiskReason="review"});
                var pos=new PaperPosition{Symbol="DOTUSDT",Quantity=5,EntryPriceBrl=4,EntryFeeBrl=.02m,StopPriceBrl=3.9m,LastPriceBrl=4,SignalClosedUtc=DateTimeOffset.UtcNow.AddMinutes(-10)};
                db.PaperPositions.Add(pos);await db.SaveChangesAsync();id=pos.Id;
                approval=new("isolated-test",id,pos.Symbol,Convert.ToBase64String((await db.PaperAccounts.SingleAsync()).RowVersion),DateTimeOffset.UtcNow,Guid.NewGuid());
            }
            await using(var db=new RacDbContext(options))
                await Assert.ThrowsAsync<InvalidOperationException>(()=>PaperRecoveryRehearsal.ApplyAsync(db,id,.6m,6,DateTimeOffset.UtcNow,""));
            await using(var db=new RacDbContext(options))
                await Assert.ThrowsAsync<InvalidOperationException>(()=>PaperRecoveryRehearsal.ApplyAsync(db,id,.7m,6,DateTimeOffset.UtcNow,"isolated-test",approval:approval,authorized:true));
            await using(var db=new RacDbContext(options))
            {
                Assert.Equal(80m,(await db.PaperAccounts.SingleAsync()).CashBrl);
                Assert.Single(await db.PaperPositions.ToListAsync());
                Assert.Empty(await db.PaperFills.ToListAsync());
                Assert.Empty(await db.AuditEvents.ToListAsync());
                Assert.Equal(0,await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM rac.RecoveryApprovalUses").SingleAsync());
            }
            await using(var db=new RacDbContext(options))
                await Assert.ThrowsAsync<InvalidOperationException>(()=>PaperRecoveryRehearsal.ApplyAsync(db,id,.6m,6,DateTimeOffset.UtcNow,"isolated-test"));
            await using(var db=new RacDbContext(options))
                await Assert.ThrowsAsync<InvalidOperationException>(()=>PaperRecoveryRehearsal.ApplyAsync(db,id,.6m,6,DateTimeOffset.UtcNow,"isolated-test",approval:approval,authorized:false));
            // Force a database failure during SaveChanges, after recovery mutations are staged.
            await using(var db=new RacDbContext(options))
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE rac.AuditEvents ADD CONSTRAINT CK_RecoveryTest_Reject CHECK (EventType <> 'PAPER_RECOVERY_REHEARSAL')");
                await Assert.ThrowsAsync<DbUpdateException>(()=>PaperRecoveryRehearsal.ApplyAsync(db,id,.6m,6,DateTimeOffset.UtcNow,"isolated-test",approval:approval,authorized:true));
            }
            await using(var db=new RacDbContext(options))
            {
                Assert.Equal(80m,(await db.PaperAccounts.SingleAsync()).CashBrl);
                Assert.Single(await db.PaperPositions.ToListAsync());
                Assert.Empty(await db.PaperFills.ToListAsync());
                Assert.Empty(await db.AuditEvents.ToListAsync());
                Assert.Equal(0,await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM rac.RecoveryApprovalUses").SingleAsync());
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE rac.AuditEvents DROP CONSTRAINT CK_RecoveryTest_Reject");
            }
            if(concurrent)
            {
                async Task<bool> Attempt()
                {
                    await using var db=new RacDbContext(options);
                    try { await PaperRecoveryRehearsal.ApplyAsync(db,id,.6m,6,DateTimeOffset.UtcNow,"isolated-test",approval:approval,authorized:true); return true; }
                    catch(InvalidOperationException) { return false; }
                    catch(DbUpdateException) { return false; }
                    catch(Microsoft.Data.SqlClient.SqlException e) when(e.Number==1205) { return false; }
                }
                var results=await Task.WhenAll(Attempt(),Attempt());
                Assert.Single(results,x=>x);
            }
            else
            {
                await using var db=new RacDbContext(options);
                await PaperRecoveryRehearsal.ApplyAsync(db,id,.6m,6,DateTimeOffset.UtcNow,"isolated-test",approval:approval,authorized:true);
            }
            await using(var db=new RacDbContext(options))
                await Assert.ThrowsAsync<InvalidOperationException>(()=>PaperRecoveryRehearsal.ApplyAsync(db,id,.6m,6,DateTimeOffset.UtcNow,"isolated-test",approval:approval,authorized:true));
            await using(var db=new RacDbContext(options))
            {
                var a=await db.PaperAccounts.SingleAsync();
                Assert.True(a.RiskLocked);Assert.False(a.Running);Assert.Equal("review",a.RiskReason);
                Assert.Equal(97.973009m,a.CashBrl);Assert.Equal(a.CashBrl,a.LastEquityBrl);
                Assert.Empty(await db.PaperPositions.ToListAsync());
                Assert.Single(await db.PaperFills.ToListAsync());Assert.Single(await db.AuditEvents.ToListAsync());
                Assert.Equal(1,await db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM rac.RecoveryApprovalUses").SingleAsync());
                Assert.Empty(await db.TestnetOrders.ToListAsync());
            }
        }
        finally{await using var db=new RacDbContext(options);await db.Database.EnsureDeletedAsync();}
    }
}
