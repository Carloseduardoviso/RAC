using System.Text.Json;
namespace RAC.Infrastructure;
public static class ResearchLicenseMetadata
{
    public sealed record License(string Doi,string Url,string? ContentVersion);
    public static IReadOnlyList<License> Parse(string json)
    {
        if(json.Length>500000)return [];
        using var doc=JsonDocument.Parse(json);
        if(doc.RootElement.ValueKind!=JsonValueKind.Object||!doc.RootElement.TryGetProperty("message",out var m)||m.ValueKind!=JsonValueKind.Object||!m.TryGetProperty("items",out var items)||items.ValueKind!=JsonValueKind.Array)return [];
        var result=new HashSet<License>();
        foreach(var item in items.EnumerateArray().Take(20))
        {
            if(item.ValueKind!=JsonValueKind.Object||!item.TryGetProperty("DOI",out var d)||d.ValueKind!=JsonValueKind.String||!item.TryGetProperty("license",out var licenses)||licenses.ValueKind!=JsonValueKind.Array)continue;
            var doi=d.GetString()!.Trim().ToLowerInvariant();
            if(doi.Length>255||!doi.StartsWith("10.",StringComparison.Ordinal)||!doi.Contains('/')||doi.Any(char.IsWhiteSpace))continue;
            foreach(var l in licenses.EnumerateArray().Take(10))
            {
                if(l.ValueKind!=JsonValueKind.Object||!l.TryGetProperty("URL",out var u)||u.ValueKind!=JsonValueKind.String)continue;
                var url=u.GetString()!;
                if(url.Length>2048||!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!uri.IsDefaultPort||uri.UserInfo.Length!=0||uri.HostNameType!=UriHostNameType.Dns)continue;
                var version=l.TryGetProperty("content-version",out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
                result.Add(new(doi,uri.AbsoluteUri,version?.Length<=100?version:null));
            }
        }
        return result.OrderBy(x=>x.Doi,StringComparer.Ordinal).ThenBy(x=>x.Url,StringComparer.Ordinal).ThenBy(x=>x.ContentVersion,StringComparer.Ordinal).ToArray();
    }
}
