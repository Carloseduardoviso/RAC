using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;
public class PaperRecoveryApprovalStoreTests
{
    [Fact]
    public async Task ConsumptionRollsBackAndCommittedIdCannotBeReused()
    {
        if(!OperatingSystem.IsWindows())return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_RECOVERY_TEST_"+Guid.NewGuid().ToString("N")+";Integrated Security=True;TrustServerCertificate=True").Options;
        await using var db=new RacDbContext(options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            await db.Database.ExecuteSqlRawAsync("CREATE TABLE rac.RecoveryApprovalUses (ApprovalId uniqueidentifier PRIMARY KEY, Reviewer nvarchar(100) NOT NULL, PositionId bigint NOT NULL, ConsumedUtc datetimeoffset NOT NULL)");
            var now=DateTimeOffset.UtcNow;
            var evidence=new PaperRecoveryApproval.Evidence("reviewer",1,"DOTUSDT","version",now,Guid.NewGuid());
            Task Consume()=>PaperRecoveryApprovalStore.ConsumeAsync(db,evidence,"reviewer",true,1,"DOTUSDT","version",now);
            await Assert.ThrowsAsync<InvalidOperationException>(Consume);
            await using(var tx=await db.Database.BeginTransactionAsync())
            {
                await Consume();
                await tx.RollbackAsync();
            }
            await using(var tx=await db.Database.BeginTransactionAsync())
            {
                await Consume();
                await tx.CommitAsync();
            }
            await using(var tx=await db.Database.BeginTransactionAsync())
            {
                var ex=await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(Consume);
                Assert.Equal(2627,ex.Number);
                await tx.RollbackAsync();
            }
        }
        finally{await db.Database.EnsureDeletedAsync();}
    }
}
