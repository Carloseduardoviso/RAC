using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
namespace RAC.Infrastructure;
public static class ResearchAbstractMetadata
{
    public sealed record Abstract(string Doi,string Text);
    public static IReadOnlyList<Abstract> Parse(string json)
    {
        if(json.Length>500000)return [];
        using var doc=JsonDocument.Parse(json);
        if(!doc.RootElement.TryGetProperty("message",out var message)||message.ValueKind!=JsonValueKind.Object||
           !message.TryGetProperty("items",out var items)||items.ValueKind!=JsonValueKind.Array)return [];
        var result=new List<Abstract>();
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in items.EnumerateArray().Take(20))
        {
            if(item.ValueKind!=JsonValueKind.Object||!item.TryGetProperty("DOI",out var d)||d.ValueKind!=JsonValueKind.String||
               !item.TryGetProperty("abstract",out var a)||a.ValueKind!=JsonValueKind.String)continue;
            var doi=d.GetString()!.Trim().ToLowerInvariant();
            var raw=a.GetString()!;
            if(doi.Length>255||!doi.StartsWith("10.",StringComparison.Ordinal)||!doi.Contains('/')||doi.Any(char.IsWhiteSpace)||raw.Length>20000)continue;
            var plain=Normalize(raw);
            if(plain is not null&&seen.Add(doi))result.Add(new(doi,plain));
        }
        return result;
    }
    private static string? Normalize(string raw)
    {
        if(string.IsNullOrWhiteSpace(raw))return null;
        string text;
        if(raw.Contains('<'))
        {
            try
            {
                using var reader=XmlReader.Create(new StringReader("<root xmlns:jats='http://www.ncbi.nlm.nih.gov/JATS1'>"+raw+"</root>"),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=21000});
                var root=XDocument.Load(reader);
                text=string.Join(" ",root.DescendantNodes().OfType<XText>().Select(x=>x.Value));
            }
            catch(XmlException){return null;}
        }
        else text=raw;
        text=string.Join(" ",text.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
        return text.Length is >0 and <=10000?text:null;
    }
}
