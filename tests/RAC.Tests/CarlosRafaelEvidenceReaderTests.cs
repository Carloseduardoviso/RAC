using RAC.Domain;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class CarlosRafaelEvidenceReaderTests
{
    [Fact]
    public void PreservesProvenanceAndRejectsUnrelatedOrStaleEvidence()
    {
        var now=DateTimeOffset.UtcNow;
        var task=Guid.NewGuid();
        RacAgentMessageRecord Row(string sender,Guid correlation,DateTimeOffset time)=>new()
        {
            Id=Guid.NewGuid(),CorrelationId=correlation,Sender=sender,Recipient="carlos",
            Kind="Report",Content="financial evidence",CreatedUtc=time
        };
        var valid=Row("rafael",task,now);
        var rows=new[]{valid,Row("samuel",task,now),Row("rafael",Guid.NewGuid(),now),
            Row("rafael",task,now.AddMinutes(-20)),Row("rafael",task,now.AddSeconds(1))};
        var evidence=Assert.Single(CarlosRafaelEvidenceReader.Select(task,rows,now,TimeSpan.FromMinutes(15)));
        Assert.Equal(valid.Id,evidence.MessageId);
        Assert.Equal(task,evidence.TaskId);
        Assert.Equal(valid.Content,evidence.Content);
        Assert.False(evidence.TradingAuthorized);
    }
}
