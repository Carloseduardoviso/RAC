using RAC.Application;
namespace RAC.Tests;
public sealed class RafaelFinancialHealthTests
{
    [Fact]
    public void RejectsStaleInconsistentStoppedOrLockedSnapshots()
    {
        var now=DateTimeOffset.UtcNow;
        var snapshot=new RafaelCapitalSnapshot(80,20,100,false);
        var age=TimeSpan.FromMinutes(2);
        RafaelFinancialHealthResult Check(RafaelCapitalSnapshot s,decimal equity,DateTimeOffset updated,DateTimeOffset? cycle,bool running)
            =>RafaelFinancialHealth.Evaluate(s,equity,updated,cycle,running,now,age,0.01m);
        var valid=Check(snapshot,100,now,now,true);
        Assert.True(valid.DataConsistent);
        Assert.True(valid.ResearchAllocationEligible);
        Assert.False(valid.TradingAuthorized);
        Assert.Contains("EQUITY_MISMATCH",Check(snapshot,101,now,now,true).Findings);
        Assert.False(Check(snapshot,100,now.AddMinutes(-3),now,true).DataConsistent);
        Assert.False(Check(snapshot,100,now,null,true).ResearchAllocationEligible);
        Assert.False(Check(snapshot,100,now,now.AddSeconds(1),true).ResearchAllocationEligible);
        Assert.False(Check(snapshot with {RiskLocked=true},100,now,now,true).ResearchAllocationEligible);
        Assert.False(Check(snapshot,100,now,now,false).ResearchAllocationEligible);
        Assert.False(Check(new(0,100,100,false),100,now,now,true).ResearchAllocationEligible);
    }
}
