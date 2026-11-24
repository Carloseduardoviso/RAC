using RAC.Infrastructure;
namespace RAC.Tests;
public class ResearchLicenseMetadataTests
{
    [Fact]
    public void ExtractsLicenseWithoutAssumingAbstractRights()
    {
        var row=Assert.Single(ResearchLicenseMetadata.Parse("""{"message":{"items":[{"DOI":"10.1234/A","license":[{"URL":"https://creativecommons.org/publicdomain/zero/1.0/","content-version":"vor"}]}]}}"""));
        Assert.Equal("10.1234/a",row.Doi);
        Assert.Equal("vor",row.ContentVersion);
        Assert.False(ResearchAbstractUsagePolicy.Evaluate(row.Url,false).PersistentStorageAllowed);
    }
    [Fact]
    public void RejectsInvalidUrls()
    {
        Assert.Empty(ResearchLicenseMetadata.Parse("""{"message":{"items":[{"DOI":"10.1234/a","license":[{"URL":"file:///secret"},{"URL":"https://user@example.com/license"}]}]}}"""));
    }
}
