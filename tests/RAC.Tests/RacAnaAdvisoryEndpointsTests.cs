using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using RAC.Api;
using RAC.Infrastructure;
using Xunit;

namespace RAC.Tests;
public sealed class RacAnaAdvisoryEndpointsTests
{
    [Fact]
    public async Task HistoryIsGetOnlyAndRequiresRiskReviewer()
    {
        var builder=WebApplication.CreateBuilder();
        builder.Services.AddDbContextFactory<RacDbContext>(o=>o.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Unused;Integrated Security=True"));
        await using var app=builder.Build();
        app.MapRacAnaAdvisories();
        var endpoint=Assert.Single(((IEndpointRouteBuilder)app).DataSources.SelectMany(x=>x.Endpoints));
        Assert.Equal("RacRiskReviewer",endpoint.Metadata.GetMetadata<IAuthorizeData>()?.Policy);
        Assert.Equal(new[]{"GET"},endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
        Assert.Equal("/api/ana/advisories",((RouteEndpoint)endpoint).RoutePattern.RawText);
    }
}
