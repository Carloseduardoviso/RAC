using RAC.Application;
using Xunit;

namespace RAC.Tests;
public sealed class RacAnaAdvisoryFingerprintTests
{
    [Fact]
    public void StableAcrossOrderingAndDuplicatesButChangesWithEvidence()
    {
        var a=Guid.NewGuid();
        var b=Guid.NewGuid();
        var first=new RacAnaAdvisoryResult("INCOMPLETE_REVIEW",["a","b"],[a,b]);
        var reordered=new RacAnaAdvisoryResult("INCOMPLETE_REVIEW",["b","a","a"],[b,a,b]);
        Assert.Equal(RacAnaAdvisoryFingerprint.Compute(first),RacAnaAdvisoryFingerprint.Compute(reordered));
        Assert.NotEqual(RacAnaAdvisoryFingerprint.Compute(first),RacAnaAdvisoryFingerprint.Compute(first with { EvidenceIds=[a] }));
        Assert.NotEqual(RacAnaAdvisoryFingerprint.Compute(first),RacAnaAdvisoryFingerprint.Compute(first with { Status="RISK_REVIEW_REQUIRED" }));
    }
}
