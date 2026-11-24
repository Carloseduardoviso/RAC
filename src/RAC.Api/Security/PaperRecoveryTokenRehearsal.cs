using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RAC.Infrastructure;

namespace RAC.Api.Security;

// Not registered in DI or exposed as an endpoint.
public sealed class PaperRecoveryTokenRehearsal(PaperRecoveryTokenService tokens)
{
    public async Task ApplyAsync(RacDbContext db,string token,ClaimsPrincipal principal,
        long positionId,decimal quote,decimal rate,DateTimeOffset observed,CancellationToken ct=default)
    {
        var connection=db.Database.GetDbConnection();
        if(!connection.Database.StartsWith("RAC_RECOVERY_TEST_",StringComparison.Ordinal) ||
           !connection.DataSource.Equals(@"(localdb)\MSSQLLocalDB",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Isolated rehearsal required.");
        var account=await db.PaperAccounts.AsNoTracking().SingleAsync(x=>x.Id==1,ct);
        var position=await db.PaperPositions.AsNoTracking().SingleAsync(x=>x.Id==positionId,ct);
        var evidence=await tokens.ReadAsync(token,principal,positionId,position.Symbol,
            Convert.ToBase64String(account.RowVersion),DateTimeOffset.UtcNow);
        await PaperRecoveryRehearsal.ApplyAsync(db,positionId,quote,rate,observed,
            evidence.Reviewer,ct,evidence,true);
    }
}
