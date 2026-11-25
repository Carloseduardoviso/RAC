using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RAC.Api;
using RAC.Infrastructure;
using Xunit;

namespace RAC.Tests;
public sealed class RacAnaAdvisoryRegistrationTests
{
    [Theory]
    [InlineData(null,false)]
    [InlineData("false",false)]
    [InlineData("true",true)]
    public void WorkerRequiresExplicitOptIn(string? value,bool expected)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string,string?> { ["Specialists:AnaAdvisory:Enabled"]=value }).Build();
        var services=new ServiceCollection();
        services.AddRacAnaAdvisory(config);
        Assert.Equal(expected,services.Any(x=>x.ServiceType==typeof(IHostedService)));
        Assert.Equal(expected,services.Any(x=>x.ServiceType==typeof(RacAnaAdvisoryCycle)));
    }
    [Theory]
    [InlineData("false","true",false)]
    [InlineData("true",null,false)]
    [InlineData("true","false",false)]
    [InlineData("true","true",true)]
    public void CarlosBatchRequiresSeparateOptIn(string enabled,string? batch,bool expected)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Specialists:AnaAdvisory:Enabled"]=enabled,
            ["Specialists:AnaAdvisory:CarlosBatchEnabled"]=batch }).Build();
        var services=new ServiceCollection();
        services.AddRacAnaAdvisory(config);
        Assert.Equal(expected,services.Any(x=>x.ServiceType==typeof(CarlosReviewRequestBatch)));
        Assert.DoesNotContain(services,x=>x.ServiceType==typeof(RacAnaReviewRequestStore));
    }
    [Fact]
    public void EnabledGraphResolvesWithSharedCalendarAndWithoutStartingWorkers()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["Specialists:AnaAdvisory:Enabled"]="true",
            ["Specialists:AnaAdvisory:ReviewRequestsEnabled"]="true",
            ["Specialists:AnaAdvisory:CarlosBatchEnabled"]="true" }).Build();
        var services=new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<RacDbContext>(o=>o.UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=RAC_DI_ONLY;Integrated Security=True"));
        services.AddSingleton<RAC.Application.RebecaNewsCalendar>();
        services.AddRacAnaAdvisory(config);
        using var provider=services.BuildServiceProvider(new ServiceProviderOptions {ValidateOnBuild=true,ValidateScopes=true});
        Assert.Same(provider.GetRequiredService<RAC.Application.RebecaNewsCalendar>(),provider.GetRequiredService<RAC.Application.IRacNewsCalendar>());
        Assert.NotNull(provider.GetRequiredService<RebecaMarketAdvisoryPublisher>());
        Assert.NotNull(provider.GetRequiredService<CarlosReviewRequestBatch>());
        Assert.NotNull(provider.GetRequiredService<RacAnaAdvisoryCycle>());
        Assert.Single(provider.GetServices<IHostedService>());
    }
}
