using System.Security.Cryptography;
using System.Text;
using RAC.Application;
namespace RAC.Infrastructure;
public sealed record RebecaMarketAdvisoryPublication(RebecaMarketAdvisoryResult Review,Guid CorrelationId,Guid MessageId,bool Published)
{
    public bool TradingAuthorized=>false;
}
// Explicit read-only evidence review followed by one idempotent advisory publication to Ana.
public sealed class RebecaMarketAdvisoryPublisher(RebecaMarketAdvisory review,RacAgentMessageStore messages)
{
    public async Task<RebecaMarketAdvisoryPublication> ReviewAndPublishAsync(Guid danielTask,Guid gabrielTask,
        DateTimeOffset now,TimeSpan maximumAge,CancellationToken ct)
    {
        var result=await review.ReviewAsync(danielTask,gabrielTask,now,maximumAge,ct);
        var correlation=CreateCorrelationId(danielTask,gabrielTask);
        var message=RebecaMarketAdvisoryReport.Create(correlation,result,now);
        var published=await messages.SaveAsync(message,ct);
        return new(result,correlation,message.Id,published);
    }
    public static Guid CreateCorrelationId(Guid danielTask,Guid gabrielTask)
    {
        if(danielTask==Guid.Empty || gabrielTask==Guid.Empty || danielTask==gabrielTask)
            throw new ArgumentException("Invalid specialist task pair.");
        var bytes=SHA256.HashData(Encoding.UTF8.GetBytes("REBECA:MARKET:CORRELATION:V1:"+
            danielTask.ToString("N")+":"+gabrielTask.ToString("N")));
        return new Guid(bytes.AsSpan(0,16));
    }
}
