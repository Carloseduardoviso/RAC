using RAC.Application;
namespace RAC.Tests;
public sealed class SamuelRoundTripTests
{
    [Fact]
    public void IncludesDepthAndFeesOnceAndRejectsIncompleteLiquidity()
    {
        var now=DateTimeOffset.UtcNow;
        var book=new SamuelDepthSnapshot(1,now,[new(99,1),new(97,1)],[new(100,1),new(102,1)]);
        var result=SamuelRoundTrip.Evaluate(book,2,1,now);
        Assert.Equal(204.02m,result.TotalEntry);
        Assert.Equal(194.04m,result.NetExit);
        Assert.Equal(-9.98m,result.NetResult);
        Assert.Equal(204.02m/1.98m,result.BreakEvenExitAverage);
        Assert.False(result.TradingAuthorized);
        var partial=SamuelRoundTrip.Evaluate(book,3,1,now);
        Assert.Equal("INSUFFICIENT_DEPTH",partial.Status);
        Assert.Null(partial.NetResult);
        Assert.Throws<ArgumentException>(()=>SamuelRoundTrip.Evaluate(book,2,1,now.AddSeconds(31)));
        Assert.Throws<ArgumentException>(()=>SamuelRoundTrip.Evaluate(book,2,100,now));
    }
}
