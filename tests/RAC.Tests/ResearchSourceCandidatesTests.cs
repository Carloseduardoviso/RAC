using RAC.Application;
namespace RAC.Tests;
public class ResearchSourceCandidatesTests
{
    [Fact]
    public void DiscoversWithoutApproving()
    {
        var c=new ResearchSourceCandidates();var now=DateTimeOffset.UtcNow;
        Assert.True(c.Observe("https://research.example.org/paper","fed",now));
        Assert.False(c.Observe("https://research.example.org/other","fed",now));
        Assert.Single(c.Snapshot());
        Assert.DoesNotContain(FinancialSourceCatalog.Sources,x=>x.HomePage.Contains("research.example.org"));
    }
    [Fact]
    public void RejectsUnsafeAndKnownUrls()
    {
        var c=new ResearchSourceCandidates();var now=DateTimeOffset.UtcNow;
        foreach(var url in new[]{"http://example.org","https://127.0.0.1","https://user@example.org","https://www.binance.com/"})
            Assert.False(c.Observe(url,"fed",now));
        Assert.Empty(c.Snapshot());
    }
}
