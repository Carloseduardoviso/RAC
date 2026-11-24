using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using RAC.Application;
using System.Text.Json;

namespace RAC.Api.Security;

// Rehearsal token issuer; not registered or exposed by the operational API.
public sealed class PaperRecoveryTokenService(IAuthorizationService authorization,IDataProtectionProvider protection)
{
    private readonly IDataProtector protector=protection.CreateProtector("RAC.PaperRecovery.Rehearsal.v1");
    public async Task<string> IssueAsync(ClaimsPrincipal principal,long positionId,string symbol,string version,DateTimeOffset now)
    {
        var reviewer=principal.Identity?.Name ?? "";
        var evidence=new PaperRecoveryApproval.Evidence(reviewer,positionId,symbol,version,now,Guid.NewGuid());
        await new PaperRecoveryReviewer(authorization).ValidateAsync(principal,evidence,positionId,symbol,version,now);
        return protector.Protect(JsonSerializer.Serialize(evidence));
    }
    public async Task<PaperRecoveryApproval.Evidence> ReadAsync(string token,ClaimsPrincipal principal,long positionId,string symbol,string version,DateTimeOffset now)
    {
        var evidence=JsonSerializer.Deserialize<PaperRecoveryApproval.Evidence>(protector.Unprotect(token))
            ?? throw new InvalidOperationException("Missing approval evidence.");
        await new PaperRecoveryReviewer(authorization).ValidateAsync(principal,evidence,positionId,symbol,version,now);
        return evidence;
    }
}
