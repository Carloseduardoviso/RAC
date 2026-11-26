using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RAC.Api;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class GabrielIndependentFreezeRegistrationTests
{
    [Theory]
    [InlineData(false,false)]
    [InlineData(true,false)]
    [InlineData(true,true)]
    public void FreezeRegistrationRequiresExplicitOptIn(bool memory,bool freeze)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Specialists:Gabriel:DynamicHistoricalEnabled"]="true",
            ["Specialists:Gabriel:QuantitativeMemoryEnabled"]=memory.ToString(),
            ["Specialists:Gabriel:IndependentFreezeEnabled"]=freeze.ToString()
        }).Build();
        var services=new ServiceCollection();
        services.AddGabrielDynamicHistoricalSource(config);
        Assert.Equal(freeze,services.Any(x=>x.ServiceType==typeof(IndependentResearchFreezeStore)));
    }
    [Fact]
    public void FreezeWithoutMemoryIsRejected()
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Specialists:Gabriel:DynamicHistoricalEnabled"]="true",
            ["Specialists:Gabriel:IndependentFreezeEnabled"]="true"
        }).Build();
        Assert.Throws<InvalidOperationException>(()=>new ServiceCollection().AddGabrielDynamicHistoricalSource(config));
    }
}
