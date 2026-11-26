using RAC.Application;
namespace RAC.Tests;
public class IndependentResearchArchiveEvidenceTests
{
    [Fact]
    public void DigestBindsAllMetricsAndFreezeIdentity()
    {
        var id=new string('A',64);var freeze=new string('B',64);
        var e=new IndependentResearchGate.Evidence(new DateOnly(2026,12,1),.003m,10,20,10,2,false);
        var digest=IndependentResearchArchiveEvidence.Digest(id,freeze,e);
        Assert.Equal(digest,IndependentResearchArchiveEvidence.Digest(id.ToLowerInvariant(),freeze,e));
        foreach(var changed in new[]{e with {Profit=21},e with {Loss=11},e with {Trades=11},e with {DrawdownPct=3},e with {Unresolved=true},e with {Cost=.0015m},e with {Month=e.Month.AddMonths(1)}})
            Assert.NotEqual(digest,IndependentResearchArchiveEvidence.Digest(id,freeze,changed));
        Assert.NotEqual(digest,IndependentResearchArchiveEvidence.Digest(id,id,e));
        Assert.Throws<ArgumentException>(()=>IndependentResearchArchiveEvidence.Digest("bad",freeze,e));
    }
    [Fact]
    public void RejectsFutureMonthsAndInvalidArchives()
    {
        var row=ResearchExperimentPlan.Build()[0];var id=new string('A',64);
        var frozen=new DateTimeOffset(2026,11,20,0,0,0,TimeSpan.Zero);
        var month=new DateOnly(2026,12,1);
        Assert.Throws<ArgumentException>(()=>IndependentResearchArchiveEvidence.Replay(row,month,[],id,id,frozen,frozen));
        Assert.Throws<InvalidDataException>(()=>IndependentResearchArchiveEvidence.Replay(row,month,[],id,id,frozen,frozen.AddMonths(2)));
    }
}
