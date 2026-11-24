using System.Text.Json;
using RAC.Application;
namespace RAC.Infrastructure;
public sealed class CrossrefResearchClient(HttpClient http)
{
    public static readonly Uri Endpoint=new("https://api.crossref.org/works?query=cryptocurrency%20quantitative%20trading%20risk&rows=20&select=URL,link");
    public static int Discover(string json,ResearchSourceCandidates candidates,DateTimeOffset now)
    {
        using var doc=JsonDocument.Parse(json);
        int added=0;
        foreach(var item in doc.RootElement.GetProperty("message").GetProperty("items").EnumerateArray().Take(20))
        {
            if(item.TryGetProperty("link",out var links)&&links.ValueKind==JsonValueKind.Array)
                foreach(var link in links.EnumerateArray().Take(5))
                    if(link.TryGetProperty("URL",out var url)&&url.ValueKind==JsonValueKind.String&&
                        candidates.Observe(url.GetString()!,"crossref",now))added++;
        }
        return added;
    }
    public async Task<int> ReadAsync(ResearchSourceCandidates candidates,CancellationToken ct)
    {
        using var response=await http.GetAsync(Endpoint,HttpCompletionOption.ResponseHeadersRead,ct);
        response.EnsureSuccessStatusCode();
        if(response.RequestMessage?.RequestUri!=Endpoint)throw new InvalidDataException("Unexpected origin.");
        await using var stream=await response.Content.ReadAsStreamAsync(ct);
        using var reader=new StreamReader(stream);
        var buffer=new char[500001];
        var count=await reader.ReadBlockAsync(buffer.AsMemory(),ct);
        if(count>500000)throw new InvalidDataException("Research response too large.");
        return Discover(new string(buffer,0,count),candidates,DateTimeOffset.UtcNow);
    }
}
