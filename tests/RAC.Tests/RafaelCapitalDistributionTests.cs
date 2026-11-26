using RAC.Application;
namespace RAC.Tests;
public sealed class RafaelCapitalDistributionTests
{
    [Fact]
    public void RespectsAggregateAndIndividualLimitsAndEvidence()
    {
        var snapshot=new RafaelCapitalSnapshot(80,20,100,false);
        var limits=new RafaelAllocationLimits(30,40,15);
        RafaelAgentEvidence[] agents=[new("a",10,1,true),new("b",0,0.8m,true),new("c",10,1,false)];
        var result=RafaelCapitalDistribution.Evaluate(snapshot,limits,agents);
        Assert.Equal(20m,result.Sum(x=>x.ResearchAmount));
        Assert.Equal(5m,result.Single(x=>x.AgentId=="a").ResearchAmount);
        Assert.Equal(15m,result.Single(x=>x.AgentId=="b").ResearchAmount);
        Assert.Equal(0m,result.Single(x=>x.AgentId=="c").ResearchAmount);
        Assert.All(result,x=>Assert.False(x.TradingAuthorized));
        Assert.All(RafaelCapitalDistribution.Evaluate(snapshot with {RiskLocked=true},limits,agents),x=>Assert.Equal(0m,x.ResearchAmount));
        Assert.All(RafaelCapitalDistribution.Evaluate(snapshot,limits,agents.Select(x=>x with {IndependentValidation=false}).ToArray()),x=>Assert.Equal(0m,x.ResearchAmount));
        Assert.Throws<ArgumentException>(()=>RafaelCapitalDistribution.Evaluate(snapshot,limits,[agents[0],agents[0]]));
    }
    [Fact]
    public void RejectsAliasDuplicatesAndPreservesReserveAcrossReallocation()
    {
        var snapshot=new RafaelCapitalSnapshot(80,20,100,false);
        var limits=new RafaelAllocationLimits(70,90,30);
        RafaelAgentEvidence[] agents=[new("a",20,1,true),new("b",0,.8m,true)];
        Assert.Throws<ArgumentException>(()=>RafaelCapitalDistribution.Evaluate(snapshot,limits,[agents[0],agents[1] with {AgentId="A"}]));
        Assert.Throws<ArgumentException>(()=>RafaelCapitalDistribution.Evaluate(snapshot,limits,[agents[0] with {AgentId=" a"}]));
        var first=RafaelCapitalDistribution.Evaluate(snapshot,limits,agents);
        Assert.Equal(10m,first.Sum(x=>x.ResearchAmount));
        var second=RafaelCapitalDistribution.Evaluate(snapshot,limits,agents.Select(x=>x with {QualityScore=1-x.QualityScore}).ToArray());
        Assert.Equal(10m,second.Sum(x=>x.ResearchAmount));
        Assert.Equal(10m,second.Single(x=>x.AgentId=="b").ResearchAmount);
        Assert.All(second,x=>Assert.False(x.TradingAuthorized));
    }
}
