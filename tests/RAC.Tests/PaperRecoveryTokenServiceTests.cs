using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using RAC.Api.Security;
using RAC.Domain;
namespace RAC.Tests;
public class PaperRecoveryTokenServiceTests
{
    [Fact]
    public async Task RealPolicyAndProtectedTokenRejectInvalidEvidence()
    {
        var services=new ServiceCollection();
        services.AddLogging();
        services.AddRacAuthentication(true);
        using var provider=services.BuildServiceProvider();
        using var scope=provider.CreateScope();
        var service=new PaperRecoveryTokenService(scope.ServiceProvider.GetRequiredService<IAuthorizationService>(),new EphemeralDataProtectionProvider());
        ClaimsPrincipal User(string name,string role)=>new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Name,name),new Claim(ClaimTypes.Role,role)},"test"));
        var reviewer=User("reviewer",RacRoles.RiskReviewer);
        var now=DateTimeOffset.UtcNow;
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.IssueAsync(User("operator",RacRoles.Operator),1,"DOTUSDT","v",now));
        var token=await service.IssueAsync(reviewer,1,"DOTUSDT","v",now);
        Assert.Equal(1L,(await service.ReadAsync(token,reviewer,1,"DOTUSDT","v",now)).PositionId);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ReadAsync(token,reviewer,2,"DOTUSDT","v",now));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ReadAsync(token,User("other",RacRoles.Administrator),1,"DOTUSDT","v",now));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ReadAsync(token,reviewer,1,"DOTUSDT","v",now.AddMinutes(3)));
        await Assert.ThrowsAsync<System.Security.Cryptography.CryptographicException>(()=>service.ReadAsync("invalid"+token,reviewer,1,"DOTUSDT","v",now));
    }
}
