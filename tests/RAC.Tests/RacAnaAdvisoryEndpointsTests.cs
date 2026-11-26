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
    public async Task AdvisoryEndpointsRequireRiskReviewerAndUseReadAndExplicitPublishMethods()
    {
        var builder=WebApplication.CreateBuilder();
        builder.Services.AddDbContextFactory<RacDbContext>(o=>o.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=Unused;Integrated Security=True"));
        await using var app=builder.Build();
        app.MapRacAnaAdvisories();
        var endpoints=((IEndpointRouteBuilder)app).DataSources.SelectMany(x=>x.Endpoints).Cast<RouteEndpoint>().ToArray();
        Assert.Equal(3,endpoints.Length);
        var audit=Assert.Single(endpoints,x=>x.RoutePattern.RawText=="/api/ana/research/freeze-audit/{configurationId}");
        Assert.Equal("RacRiskReviewer",audit.Metadata.GetMetadata<IAuthorizeData>()?.Policy);
        Assert.Equal(new[]{"GET"},audit.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
        var get=Assert.Single(endpoints,x=>x.RoutePattern.RawText=="/api/ana/advisories");
        Assert.Equal("RacRiskReviewer",get.Metadata.GetMetadata<IAuthorizeData>()?.Policy);
        Assert.Equal(new[]{"GET"},get.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
        var publish=Assert.Single(endpoints,x=>x.RoutePattern.RawText=="/api/ana/rebeca/market-review");
        Assert.Equal("RacRiskReviewer",publish.Metadata.GetMetadata<IAuthorizeData>()?.Policy);
        Assert.Equal(new[]{"POST"},publish.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods);
    }
}
