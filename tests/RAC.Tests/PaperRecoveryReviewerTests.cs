using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using RAC.Api.Security;
using RAC.Application;
namespace RAC.Tests;
public class PaperRecoveryReviewerTests
{
    [Theory]
    [InlineData(true,true,true)]
    [InlineData(false,true,false)]
    [InlineData(true,false,false)]
    public async Task RequiresAuthenticatedPolicySuccess(bool authenticated,bool allowed,bool expected)
    {
        var principal=new ClaimsPrincipal(new ClaimsIdentity(
            new[]{new Claim(ClaimTypes.Name,"reviewer")},authenticated?"test":null));
        var now=DateTimeOffset.UtcNow;
        var evidence=new PaperRecoveryApproval.Evidence("reviewer",1,"DOTUSDT","v",now,Guid.NewGuid());
        var service=new PaperRecoveryReviewer(new Policy(allowed));
        Task<string> Check()=>service.ValidateAsync(principal,evidence,1,"DOTUSDT","v",now);
        if(expected)Assert.Equal("reviewer",await Check());
        else await Assert.ThrowsAsync<InvalidOperationException>(Check);
    }
    private sealed class Policy(bool allowed):IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user,object? resource,string policyName)
        {
            Assert.Equal("RacRiskReviewer",policyName);
            return Task.FromResult(allowed?AuthorizationResult.Success():AuthorizationResult.Failed());
        }
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user,object? resource,IEnumerable<IAuthorizationRequirement> requirements)
            =>throw new NotSupportedException();
    }
}
