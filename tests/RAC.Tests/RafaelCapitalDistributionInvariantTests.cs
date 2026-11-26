using RAC.Application;
namespace RAC.Tests;
public sealed class RafaelCapitalDistributionInvariantTests
{
    [Fact]
    public void DistributionRespectsConsolidatedCapacityAcrossScenarios()
    {
        var random=new Random(20261126);
        for(var i=0;i<500;i++)
        {
            decimal cash=random.Next(0,10000), marked=random.Next(0,10000);
            var snapshot=new RafaelCapitalSnapshot(cash,marked,cash+marked,i%7==0);
            var limits=new RafaelAllocationLimits(random.Next(0,101),random.Next(0,101),random.Next(0,101));
            var agents=Enumerable.Range(0,12).Select(j=>new RafaelAgentEvidence($"agent-{j}",marked/24,random.Next(0,101)/100m,j%3!=0)).ToArray();
            var capacity=RafaelAllocationCapacity.Evaluate(snapshot,limits);
            var result=RafaelCapitalDistribution.Evaluate(snapshot,limits,agents);
            Assert.Equal(12,result.Count);
            Assert.InRange(result.Sum(x=>x.ResearchAmount),0,Math.Min(capacity.CashHeadroom,capacity.ExposureHeadroom));
            foreach(var allocation in result)
            {
                var agent=agents.Single(x=>x.AgentId==allocation.AgentId);
                Assert.False(allocation.TradingAuthorized);
                Assert.InRange(allocation.ResearchAmount,0,Math.Max(0,capacity.SinglePositionCeiling-agent.ExistingExposure));
                if(snapshot.RiskLocked || !agent.IndependentValidation || agent.QualityScore==0)Assert.Equal(0m,allocation.ResearchAmount);
            }
            Assert.Equal(result.ToArray(),RafaelCapitalDistribution.Evaluate(snapshot,limits,agents.Reverse().ToArray()).ToArray());
        }
    }
}
