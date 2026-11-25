using RAC.Application;
using RAC.Infrastructure;
using Xunit;

public class RacAgentMessageStoreTests
{
    [Fact]
    public void PreservesMessageIdentityAndNormalizesTime()
    {
        var m = RacAgentCommunication.Create("rebeca","daniel",RacMessageKind.TaskRequest,
            "Research",Guid.NewGuid(),DateTimeOffset.Now);
        var row = RacAgentMessageStore.Validate(m);
        Assert.Equal(m.Id,row.Id);
        Assert.Equal(m.CorrelationId,row.CorrelationId);
        Assert.Equal(TimeSpan.Zero,row.CreatedUtc.Offset);
        Assert.Equal("TaskRequest",row.Kind);
    }
    [Fact]
    public void RejectsForgedRouteAndMissingId()
    {
        var m = RacAgentCommunication.Create("rebeca","daniel",RacMessageKind.TaskRequest,
            "Research",Guid.NewGuid(),DateTimeOffset.UtcNow);
        Assert.Throws<InvalidOperationException>(() => RacAgentMessageStore.Validate(m with { Sender = "lucas" }));
        Assert.Throws<ArgumentException>(() => RacAgentMessageStore.Validate(m with { Id = Guid.Empty }));
    }
}
