using RAC.Application;
namespace RAC.Infrastructure;
public sealed class CarlosFinancialAdvisoryPublisher(CarlosFinancialEvidenceService service,RacAgentMessageStore messages)
{
    public async Task<RacAgentMessage> PublishAsync(Guid correlationId,Guid rafaelTask,Guid samuelTask,Guid miguelTask,
        DateTimeOffset observedUtc,TimeSpan maximumAge,CancellationToken ct)
    {
        if(correlationId==Guid.Empty) throw new ArgumentException("Invalid advisory correlation.");
        var result=await service.ReviewAsync(rafaelTask,samuelTask,miguelTask,observedUtc,maximumAge,ct);
        var message=CarlosFinancialAdvisoryReport.Create(correlationId,result,observedUtc);
        var existing=(await messages.ReadInboxAsync("ana",correlationId,500,ct)).SingleOrDefault(x=>x.Id==message.Id);
        if(existing is not null)
        {
            if(existing.Content!=message.Content || existing.Sender!=message.Sender ||
                existing.Recipient!=message.Recipient || existing.Kind!=message.Kind.ToString())
                throw new InvalidOperationException("Advisory identity collision.");
            return message with {CreatedUtc=existing.CreatedUtc};
        }
        await messages.SaveAsync(message,ct);
        return message;
    }
}
