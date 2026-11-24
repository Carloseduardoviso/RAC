using RAC.Infrastructure;
namespace RAC.Tests;
public class CrossrefPublicationMetadataTests
{
    [Fact]
    public void ParsesAndDeduplicatesDoi()
    {
        var rows=CrossrefPublicationMetadata.Parse("""{"message":{"items":[{"DOI":"10.1234/a","title":["Research"],"author":[{"given":"A","family":"B"}],"published":{"date-parts":[[2024]]}},{"DOI":"10.1234/A","title":["Duplicate"]},{"DOI":"invalid","title":["Invalid"]}]}}""");
        var row=Assert.Single(rows);
        Assert.Equal("Research",row.Title);
        Assert.Equal("A B",Assert.Single(row.Authors));
        Assert.Equal(2024,row.Year);
    }
}
