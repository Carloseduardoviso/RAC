using RAC.Application;
namespace RAC.Tests;
public class FinancialSourceCatalogTests
{
    [Fact]
    public void CatalogHasUniqueIdsAndHttpsHomepages()
    {
        var sources=FinancialSourceCatalog.Sources;
        Assert.Equal(sources.Count,sources.Select(x=>x.Id).Distinct().Count());
        Assert.All(sources,x=>Assert.Equal("https",new Uri(x.HomePage).Scheme));
        Assert.Contains(sources,x=>x.Id=="binance");
        Assert.Contains(sources,x=>x.Id=="b3");
        Assert.Contains(sources,x=>x.Id=="google-news");
        Assert.Contains(sources,x=>x.Category=="INVESTOR_RESEARCH");
    }
}
