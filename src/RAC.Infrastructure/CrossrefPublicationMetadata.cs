using System.Text.Json;
namespace RAC.Infrastructure;
public static class CrossrefPublicationMetadata
{
    public sealed record Publication(string Doi,string Title,IReadOnlyList<string> Authors,int? Year);
    private static string Read(JsonElement item,string key,int max)
    {
        if(!item.TryGetProperty(key,out var value)||value.ValueKind!=JsonValueKind.String)return "";
        var s=value.GetString()!.Trim();
        return s.Length<=max?s:"";
    }
    public static IReadOnlyList<Publication> Parse(string json)
    {
        using var doc=JsonDocument.Parse(json);
        var result=new Dictionary<string,Publication>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in doc.RootElement.GetProperty("message").GetProperty("items").EnumerateArray().Take(20))
        {
            if(item.ValueKind!=JsonValueKind.Object)continue;
            var doi=Read(item,"DOI",255);
            if(!doi.StartsWith("10.")||!doi.Contains('/')||doi.Any(char.IsWhiteSpace))continue;
            var title="";
            if(item.TryGetProperty("title",out var titles)&&titles.ValueKind==JsonValueKind.Array&&titles.GetArrayLength()>0&&titles[0].ValueKind==JsonValueKind.String)
                title=titles[0].GetString()!;
            if(string.IsNullOrWhiteSpace(title)||title.Length>1000)continue;
            var authors=new List<string>();
            if(item.TryGetProperty("author",out var list)&&list.ValueKind==JsonValueKind.Array)
                foreach(var author in list.EnumerateArray().Take(50))
                    if(author.ValueKind==JsonValueKind.Object)
                    {
                        var name=(Read(author,"given",200)+" "+Read(author,"family",200)).Trim();
                        if(name.Length>0)authors.Add(name);
                    }
            int? year=null;
            if(item.TryGetProperty("published",out var published)&&published.ValueKind==JsonValueKind.Object&&published.TryGetProperty("date-parts",out var parts)&&parts.ValueKind==JsonValueKind.Array&&parts.GetArrayLength()>0&&parts[0].ValueKind==JsonValueKind.Array&&parts[0].GetArrayLength()>0&&parts[0][0].ValueKind==JsonValueKind.Number&&parts[0][0].TryGetInt32(out var y)&&y>=1600&&y<=DateTime.UtcNow.Year+1)year=y;
            result.TryAdd(doi,new(doi,title,authors.AsReadOnly(),year));
        }
        return result.Values.ToArray();
    }
}
