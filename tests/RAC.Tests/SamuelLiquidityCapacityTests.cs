using RAC.Application;
namespace RAC.Tests;
public sealed class SamuelLiquidityCapacityTests
{
    [Fact]
    public void ClassifiesIndependentScenarios()
    {
        var now=DateTimeOffset.UtcNow;
        var book=new SamuelDepthSnapshot(1,now,[new(99,1),new(97,1)],[new(100,1),new(102,1)]);
        var rows=SamuelLiquidityCapacity.Evaluate(book,[3,1,2],0,0.5m,2,now);
        Assert.Equal("RESEARCH_ONLY",rows[0].Status);
        Assert.Equal("EXCESSIVE_IMPACT",rows[1].Status);
        Assert.Equal("INSUFFICIENT_DEPTH",rows[2].Status);
        Assert.All(rows,r=>Assert.False(r.TradingAuthorized));
        Assert.Equal("EXCESSIVE_COST",SamuelLiquidityCapacity.Evaluate(book,[1],1,2,2,now)[0].Status);
        Assert.Throws<ArgumentException>(()=>SamuelLiquidityCapacity.Evaluate(book,[1,1],0,1,1,now));
        Assert.Throws<ArgumentException>(()=>SamuelLiquidityCapacity.Evaluate(book,[1],0,-1,1,now));
    }
}
