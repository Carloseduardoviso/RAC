using RAC.Domain;
using RAC.Infrastructure;
using Xunit;
namespace RAC.Tests;
public sealed class RebecaMarketEvidenceReaderTests
{
    [Fact]
    public void SelectsOnlyFreshReportsFromRequestedSpecialist()
    {
        var now=DateTimeOffset.UtcNow;
        var task=Guid.NewGuid();
        RacAgentMessageRecord Row(string sender,DateTimeOffset at,string recipient="rebeca",string kind="Report") =>
            new(){Id=Guid.NewGuid(),CorrelationId=task,Sender=sender,Recipient=recipient,Kind=kind,CreatedUtc=at,Content="{}"};
        var daniel=Row("daniel",now);
        var gabriel=Row("gabriel",now);
        var rows=new[]{daniel,gabriel,Row("daniel",now.AddMinutes(-6)),Row("daniel",now.AddSeconds(1)),
            Row("daniel",now,"ana"),Row("daniel",now,kind:"TaskRequest")};
        var d=Assert.Single(RebecaMarketEvidenceReader.Select("daniel",task,rows,now,TimeSpan.FromMinutes(5)));
        var g=Assert.Single(RebecaMarketEvidenceReader.Select("gabriel",task,rows,now,TimeSpan.FromMinutes(5)));
        Assert.Equal(daniel.Id,d.MessageId);
        Assert.Equal(gabriel.Id,g.MessageId);
        Assert.False(d.TradingAuthorized);
        Assert.False(g.TradingAuthorized);
        Assert.Throws<ArgumentException>(()=>RebecaMarketEvidenceReader.Select("carlos",task,rows,now,TimeSpan.FromMinutes(5)));
        Assert.Throws<ArgumentException>(()=>RebecaMarketEvidenceReader.Select("daniel",task,rows,now,TimeSpan.FromDays(2)));
    }
}
