using RAC.Application;
using Xunit;
namespace RAC.Tests;
public sealed class RacAnaReportReviewTests
{
    [Fact]
    public void RejectsStaleFutureForeignAndUnauthorizedReports()
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        RacAgentMessage Report(string sender, Guid correlation, DateTimeOffset at) =>
            new(Guid.NewGuid(), correlation, sender, "ana", RacMessageKind.Report, "evidence", at);
        var result = RacAnaReportReview.Review(id, new[] {
            Report("rebeca", id, now), Report("carlos", id, now.AddHours(-2)),
            Report("lucas", id, now.AddMinutes(1)), Report("lucas", Guid.NewGuid(), now),
            Report("daniel", id, now)}, now, TimeSpan.FromHours(1));
        Assert.Single(result.Evidence);
        Assert.Equal(new[] {"carlos","lucas"}, result.Missing);
    }
    [Fact]
    public void PreservesConflictingEvidenceAndRiskNotice()
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        RacAgentMessage Message(string sender, RacMessageKind kind, string content) =>
            RacAgentCommunication.Create(sender,"ana",kind,content,id,now);
        var result = RacAnaReportReview.Review(id, new[] {
            Message("rebeca", RacMessageKind.Report,"first"),
            Message("rebeca", RacMessageKind.Report,"second"),
            Message("carlos", RacMessageKind.Report,"risk"),
            Message("lucas", RacMessageKind.Report,"health"),
            Message("carlos", RacMessageKind.RiskNotice,"stop")}, now, TimeSpan.FromMinutes(5));
        Assert.Empty(result.Missing);
        Assert.Equal(new[] {"rebeca"}, result.Conflicting);
        Assert.True(result.RiskNoticePresent);
        Assert.Equal(4,result.Evidence.Count);
    }
}
