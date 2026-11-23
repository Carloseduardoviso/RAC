using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using RAC.Api.Security;
using RAC.Domain;

namespace RAC.Tests;

public sealed class ProductionRiskPolicyAuthorizationTests
{
    [Fact]
    public async Task Policy_routes_require_independent_roles()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRacAuthentication(development: true);
        using var provider = services.BuildServiceProvider();
        var policies = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var propose = await policies.GetPolicyAsync("RacRiskReviewer");
        var review = await policies.GetPolicyAsync("RacWalletOwner");
        Assert.NotNull(propose);
        Assert.NotNull(review);
        var proposalRoles = Assert.Single(propose.Requirements
            .OfType<RolesAuthorizationRequirement>());
        Assert.Contains(RacRoles.Administrator, proposalRoles.AllowedRoles);
        Assert.Contains(RacRoles.RiskReviewer, proposalRoles.AllowedRoles);
        var reviewRoles = Assert.Single(review.Requirements
            .OfType<RolesAuthorizationRequirement>());
        Assert.Equal(new[] { RacRoles.Administrator }, reviewRoles.AllowedRoles);
        Assert.Contains(propose.Requirements, r =>
            r is DenyAnonymousAuthorizationRequirement);
        Assert.Contains(review.Requirements, r =>
            r is DenyAnonymousAuthorizationRequirement);
    }
}
