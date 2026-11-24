using RAC.Infrastructure;
namespace RAC.Tests;
public class ResearchAbstractMetadataTests
{
    [Fact]
    public void ExtractsPlainTextFromJats()
    {
        var rows=ResearchAbstractMetadata.Parse("""{"message":{"items":[{"DOI":"10.1234/ABC","abstract":"<jats:p>Machine learning</jats:p><jats:p>Validation</jats:p>"}]}}""");
        var row=Assert.Single(rows);
        Assert.Equal("10.1234/abc",row.Doi);
        Assert.Equal("Machine learning Validation",row.Text);
    }
    [Fact]
    public void RejectsMalformedMarkupAndMissingAbstract()
    {
        Assert.Empty(ResearchAbstractMetadata.Parse("""{"message":{"items":[{"DOI":"10.1234/a","abstract":"<!DOCTYPE x SYSTEM 'file:///secret'><x/>"},{"DOI":"10.1234/b"},{"DOI":"10.1234/c","abstract":"<p>broken"}]}}"""));
    }
}
