using RAC.Infrastructure;
namespace RAC.Tests;
public class ResearchTitleEvidenceTests
{
    [Fact]
    public void ExtractsOnlyExplicitTitleMentions()
    {
        var hints=ResearchTitleEvidence.Extract("Machine learning with out-of-sample validation and transaction costs");
        Assert.Equal(3,hints.Count);
        Assert.Contains(hints,x=>x.Category=="VALIDATION"&&x.Evidence=="out-of-sample");
        Assert.DoesNotContain(hints,x=>x.Category=="INDICATOR");
    }
    [Fact]
    public void RejectsSubstringAndMissingEvidence()
    {
        Assert.Empty(ResearchTitleEvidence.Extract("University research"));
        Assert.Empty(ResearchTitleEvidence.Extract(null));
        Assert.Empty(ResearchTitleEvidence.Extract(new string('a',1001)));
    }
}
