using RAC.Application;
using Xunit;

public class RacAgentCommunicationTests
{
    [Theory]
    [InlineData("rebeca", "daniel", RacMessageKind.TaskRequest)]
    [InlineData("daniel", "rebeca", RacMessageKind.Report)]
    [InlineData("carlos", "ana", RacMessageKind.RiskNotice)]
    public void AllowsReportingLines(string from, string to, RacMessageKind kind)
    {
        var correlation = Guid.NewGuid();
        var message = RacAgentCommunication.Create(from,to,kind,"Research only",correlation,DateTimeOffset.UtcNow);
        Assert.Equal(correlation,message.CorrelationId);
        Assert.NotEqual(Guid.Empty,message.Id);
    }
    [Theory]
    [InlineData("daniel","rafael",RacMessageKind.TaskRequest)]
    [InlineData("daniel","ana",RacMessageKind.Report)]
    [InlineData("lucas","ana",RacMessageKind.RiskNotice)]
    public void RejectsUnauthorizedRoutes(string from,string to,RacMessageKind kind)
    {
        Assert.Throws<InvalidOperationException>(() => RacAgentCommunication.Create(from,to,kind,"Report",Guid.NewGuid(),DateTimeOffset.UtcNow));
    }
    [Fact]
    public void RejectsUnknownAgent()
    {
        Assert.Throws<ArgumentException>(() => RacAgentCommunication.Create("unknown","ana",RacMessageKind.Report,"Report",Guid.NewGuid(),DateTimeOffset.UtcNow));
    }
}
