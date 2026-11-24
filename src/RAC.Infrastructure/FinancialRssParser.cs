using System.Xml;
using System.Xml.Linq;
namespace RAC.Infrastructure;

// Read-only RSS parser; never treats headlines as trading signals.
public static class FinancialRssParser
{
    public sealed record Article(string Source,string Title,string Url,DateTimeOffset PublishedUtc);
    public static IReadOnlyList<Article> Parse(string xml,string source,DateTimeOffset now)
    {
        using var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{
            DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1000000});
        var doc=XDocument.Load(reader);
        var result=new List<Article>();
        foreach(var item in doc.Descendants("item").Take(200))
        {
            var title=((string?)item.Element("title"))?.Trim();
            var link=((string?)item.Element("link"))?.Trim();
            if(string.IsNullOrWhiteSpace(title)||title.Length>500||
                !Uri.TryCreate(link,UriKind.Absolute,out var uri)||uri.Scheme!="https"||
                !string.IsNullOrEmpty(uri.UserInfo)||
                !DateTimeOffset.TryParse((string?)item.Element("pubDate"),System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal,out var published)||
                published>now||now-published>TimeSpan.FromDays(7))continue;
            result.Add(new(source,title,uri.AbsoluteUri,published.ToUniversalTime()));
        }
        return result.DistinctBy(x=>x.Url).ToArray();
    }
}
