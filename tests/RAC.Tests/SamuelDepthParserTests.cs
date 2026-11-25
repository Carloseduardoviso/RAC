using RAC.Application;
namespace RAC.Tests;
public sealed class SamuelDepthParserTests
{
    [Fact]
    public void ValidatesPublicSnapshotAndReceiptAge()
    {
        const string json="""{"lastUpdateId":123,"bids":[["99","2"],["98","1"]],"asks":[["100","3"]]}""";
        var now=DateTimeOffset.UtcNow;
        var book=SamuelDepthParser.Parse(json,now,now);
        Assert.Equal(123L,book.UpdateId);
        Assert.Equal(99m,book.Bids[0].Price);
        Assert.Equal(3m,book.Asks[0].Quantity);
        Assert.False(book.TradingAuthorized);
        Assert.Throws<ArgumentException>(()=>SamuelDepthParser.Parse(json,now.AddSeconds(-31),now));
        Assert.Throws<ArgumentException>(()=>SamuelDepthParser.Parse(json,now.AddSeconds(1),now));
        Assert.Throws<ArgumentException>(()=>SamuelDepthParser.Parse(json.Replace("100","98"),now,now));
    }
}
