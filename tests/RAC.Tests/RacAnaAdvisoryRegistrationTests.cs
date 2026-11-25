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
}
