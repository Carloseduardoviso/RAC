using RAC.Infrastructure;
namespace RAC.Tests;
public class ResearchAbstractUsagePolicyTests
{
    [Theory]
    [InlineData("https://creativecommons.org/publicdomain/zero/1.0/",false,false)]
    [InlineData("https://creativecommons.org/publicdomain/zero/1.0/",true,true)]
    [InlineData("https://creativecommons.org/licenses/by/4.0/",true,false)]
    [InlineData("https://creativecommons.org.example.com/publicdomain/zero/1.0/",true,false)]
    [InlineData(null,true,false)]
    public void RequiresVerifiedAbstractScopeAndSupportedLicense(string? url,bool scope,bool allowed)
    {
        var decision=ResearchAbstractUsagePolicy.Evaluate(url,scope);
        Assert.Equal(allowed,decision.PersistentStorageAllowed);
        Assert.False(decision.TradingAuthorized);
    }
}
