using RAC.Application;
namespace RAC.Tests;
public class IndependentResearchReplayEvidenceTests
{
    [Fact]
    public void ReplayMapsBothCostsAndRejectsIncompleteOrHistoricalMonths()
    {
        var plan=ResearchExperimentPlan.Build();
        var stress=plan.First(x=>x.Parameters.SlippagePctPerSide==.20m);
        var normal=stress with {Parameters=stress.Parameters with {SlippagePctPerSide=.05m}};
        var month=new DateOnly(2026,12,1);
        var data=Enumerable.Repeat(new ResearchExperimentReplay.Minute(100,100,1000000),31*1440).ToArray();
        foreach(var row in new[]{stress,normal})
        {
            var evidence=IndependentResearchGate.ReplayMonth(month,row,data);
            Assert.Equal((row.Parameters.FeePctPerSide+row.Parameters.SlippagePctPerSide)/100,evidence.Cost);
            Assert.Equal(0,evidence.Profit);
            Assert.Equal(0,evidence.Loss);
            Assert.Equal(0,evidence.Trades);
            Assert.Throws<ArgumentException>(()=>IndependentResearchGate.ReplayMonth(month,row,data.Skip(1).ToArray()));
            Assert.Throws<ArgumentException>(()=>IndependentResearchGate.ReplayMonth(month.AddMonths(-1),row,data));
        }
    }
}
