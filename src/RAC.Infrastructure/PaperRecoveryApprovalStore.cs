using Microsoft.EntityFrameworkCore;
using RAC.Application;

namespace RAC.Infrastructure;

// Test-only approval consumption. Caller must supply trusted authorization evidence.
public static class PaperRecoveryApprovalStore
{
    public static async Task ConsumeAsync(RacDbContext db, PaperRecoveryApproval.Evidence evidence,
        string reviewer,bool authorized,long positionId,string symbol,string version,
        DateTimeOffset now,CancellationToken ct=default)
    {
        var c=db.Database.GetDbConnection();
        if(!c.Database.StartsWith("RAC_RECOVERY_TEST_",StringComparison.Ordinal) ||
           !c.DataSource.Equals(@"(localdb)\MSSQLLocalDB",StringComparison.OrdinalIgnoreCase) ||
           db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Isolated transaction required.");
        PaperRecoveryApproval.Validate(evidence,reviewer,authorized,positionId,symbol,version,now);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO rac.RecoveryApprovalUses (ApprovalId,Reviewer,PositionId,ConsumedUtc) VALUES ({evidence.ApprovalId},{reviewer},{positionId},{now})",ct);
    }
}
