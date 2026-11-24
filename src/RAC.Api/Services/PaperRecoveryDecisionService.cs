using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Infrastructure;
using System.Text.Json;
namespace RAC.Api.Services;

// Records a recommendation only; never changes account or order state.
public sealed class PaperRecoveryDecisionService(IDbContextFactory<RacDbContext> factory, IAuthorizationService authorization)
{
    public sealed record Decision(string Recommendation, string Justification, string AccountVersion);
    public async Task RecordAsync(ClaimsPrincipal principal, Decision decision, CancellationToken ct)
    {
        var reviewer=principal.Identity?.Name;
        if(principal.Identity?.IsAuthenticated!=true || string.IsNullOrWhiteSpace(reviewer) ||
           !(await authorization.AuthorizeAsync(principal,null,"RacRiskReviewer")).Succeeded)
            throw new UnauthorizedAccessException();
        if(decision.Recommendation is not ("KEEP_LOCKED" or "REQUEST_RECOVERY_REVIEW") ||
           string.IsNullOrWhiteSpace(decision.Justification) || decision.Justification.Trim().Length<20 ||
           decision.Justification.Length>500)
            throw new ArgumentException("Invalid recommendation or justification.");
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var account=await db.PaperAccounts.AsNoTracking().SingleAsync(x=>x.Id==1,ct);
        if(account.Running || !account.RiskLocked || Convert.ToBase64String(account.RowVersion)!=decision.AccountVersion)
            throw new InvalidOperationException("Review snapshot changed.");
        db.AuditEvents.Add(new AuditEvent { EventType="PAPER_RECOVERY_DECISION",
            Description=JsonSerializer.Serialize(new { reviewer, decision.Recommendation,
                justification=decision.Justification.Trim(), decision.AccountVersion, unlockPermitted=false }) });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
