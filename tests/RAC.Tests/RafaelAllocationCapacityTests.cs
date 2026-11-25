using RAC.Application;
namespace RAC.Tests;

public sealed class RafaelAllocationCapacityTests
{
    [Fact]
    public void AppliesAllCeilingsAndRiskLock()
    {
        var limits=new RafaelAllocationLimits(30,40,15);
        var result=RafaelAllocationCapacity.Evaluate(new(80,20,100,false),limits);
        Assert.Equal(30m,result.ReserveRequired);
        Assert.Equal(20m,result.ExposureHeadroom);
        Assert.Equal(50m,result.CashHeadroom);
        Assert.Equal(15m,result.ResearchCapacity);
        Assert.False(result.TradingAuthorized);
        Assert.Equal(0m,RafaelAllocationCapacity.Evaluate(new(80,20,100,true),limits).ResearchCapacity);
        Assert.Equal(0m,RafaelAllocationCapacity.Evaluate(new(50,50,100,false),limits).ResearchCapacity);
        Assert.Equal(5m,RafaelAllocationCapacity.Evaluate(new(35,65,100,false),new(30,100,20)).ResearchCapacity);
        Assert.Equal(0m,RafaelAllocationCapacity.Evaluate(new(0,0,0,false),limits).ResearchCapacity);
        Assert.Throws<ArgumentException>(()=>RafaelAllocationCapacity.Evaluate(new(100,0,100,false),new(-1,40,15)));
    }
}
