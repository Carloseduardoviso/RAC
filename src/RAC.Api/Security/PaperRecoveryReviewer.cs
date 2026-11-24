using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using RAC.Application;

namespace RAC.Api.Security;

// Adapter for rehearsal only. No route or approval issuance is enabled.
public sealed class PaperRecoveryReviewer(IAuthorizationService authorization)
{
    public async Task<string> ValidateAsync(ClaimsPrincipal principal,
        PaperRecoveryApproval.Evidence evidence,long positionId,string symbol,
        string version,DateTimeOffset now)
    {
        var result=await authorization.AuthorizeAsync(principal,null,"RacRiskReviewer");
        var reviewer=principal.Identity?.Name ?? "";
        PaperRecoveryApproval.Validate(evidence,reviewer,
            principal.Identity?.IsAuthenticated==true && result.Succeeded,
            positionId,symbol,version,now);
        return reviewer;
    }
}
