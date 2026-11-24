namespace RAC.Infrastructure;

// Fixed public endpoint; no caller-supplied URLs or credentials.
public sealed class FederalReserveNewsClient(HttpClient http)
{
    public static readonly Uri FeedUri=new("https://www.federalreserve.gov/feeds/press_all.xml");
    public async Task<IReadOnlyList<FinancialRssParser.Article>> ReadAsync(CancellationToken ct)
    {
        using var response=await http.GetAsync(FeedUri,HttpCompletionOption.ResponseHeadersRead,ct);
        response.EnsureSuccessStatusCode();
        if(response.RequestMessage?.RequestUri!=FeedUri)
            throw new InvalidDataException("Unexpected feed origin.");
        await using var stream=await response.Content.ReadAsStreamAsync(ct);
        using var reader=new StreamReader(stream);
        var buffer=new char[1000001];
        var count=await reader.ReadBlockAsync(buffer.AsMemory(),ct);
        if(count>1000000)throw new InvalidDataException("Feed too large.");
        return FinancialRssParser.Parse(new string(buffer,0,count),"fed",DateTimeOffset.UtcNow)
            .Where(x=>new Uri(x.Url).Host=="www.federalreserve.gov").ToArray();
    }
}
