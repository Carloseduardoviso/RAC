using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RAC.Api;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class GabrielDynamicHistoricalRegistrationTests
{
    [Theory]
    [InlineData(null,false)]
    [InlineData("false",false)]
    [InlineData("true",true)]
    public void DynamicSourceRequiresExplicitOptIn(string? setting,bool enabled)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
            {["Specialists:Gabriel:DynamicHistoricalEnabled"]=setting}).Build();
        var services=new ServiceCollection();
        services.AddGabrielDynamicHistoricalSource(config);
        using var provider=services.BuildServiceProvider();
        Assert.Equal(enabled,provider.GetService<GabrielDynamicHistoricalSource>() is not null);
        Assert.Equal(enabled,provider.GetService<Func<IReadOnlyList<string>,CancellationToken,Task<string>>>() is not null);
    }
}
