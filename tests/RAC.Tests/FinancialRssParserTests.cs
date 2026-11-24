using RAC.Infrastructure;
namespace RAC.Tests;
public class FinancialRssParserTests
{
    [Fact]
    public void RejectsDtd()
    {
        Assert.Throws<System.Xml.XmlException>(()=>FinancialRssParser.Parse("<!DOCTYPE rss [<!ENTITY x SYSTEM 'file:///secret'>]><rss/>","fed",DateTimeOffset.UtcNow));
    }
    [Fact]
    public void FiltersAndDeduplicates()
    {
        var now=new DateTimeOffset(2026,11,24,12,0,0,TimeSpan.Zero);
        var item="<item><title>Release</title><link>https://www.federalreserve.gov/news.htm</link><pubDate>Tue, 24 Nov 2026 11:00:00 GMT</pubDate></item>";
        var xml="<rss><channel>"+item+item+item.Replace("https:","http:")+item.Replace("11:00:00","13:00:00")+"</channel></rss>";
        Assert.Single(FinancialRssParser.Parse(xml,"fed",now));
    }
}
