using System.Globalization;
using System.Text.Json;
namespace RAC.Application;
public sealed record SamuelDepthSnapshot(long UpdateId, DateTimeOffset ReceivedUtc,
    IReadOnlyList<SamuelBookLevel> Bids,IReadOnlyList<SamuelBookLevel> Asks)
{
    public bool TradingAuthorized=>false;
}
public static class SamuelDepthParser
{
    public static SamuelDepthSnapshot Parse(string json,DateTimeOffset receivedUtc,DateTimeOffset now)
    {
        if(receivedUtc>now || now-receivedUtc>TimeSpan.FromSeconds(30))
            throw new ArgumentException("Stale or future receipt.");
        if(json.Length>1000000) throw new ArgumentException("Oversized snapshot.");
        using var doc=JsonDocument.Parse(json);
        var root=doc.RootElement;
        var id=root.GetProperty("lastUpdateId").GetInt64();
        var bids=Read(root.GetProperty("bids"));
        var asks=Read(root.GetProperty("asks"));
        if(id<=0 || bids.Count==0 || asks.Count==0 || bids[0].Price>=asks[0].Price)
            throw new ArgumentException("Invalid or crossed book.");
        SamuelBookDepth.Evaluate(bids,1,false);
        SamuelBookDepth.Evaluate(asks,1,true);
        return new(id,receivedUtc,bids.AsReadOnly(),asks.AsReadOnly());
    }
    private static List<SamuelBookLevel> Read(JsonElement array)
    {
        if(array.GetArrayLength()>1000) throw new ArgumentException("Too many levels.");
        return array.EnumerateArray().Select(x=>{
            if(x.GetArrayLength()!=2) throw new ArgumentException("Invalid level.");
            return new SamuelBookLevel(decimal.Parse(x[0].GetString()!,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture),
                decimal.Parse(x[1].GetString()!,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture));
        }).ToList();
    }
}
