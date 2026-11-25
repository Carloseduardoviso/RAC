using RAC.Application;
namespace RAC.Infrastructure;
public sealed class CarlosFinancialAdvisoryPublisher(CarlosFinancialEvidenceService service,RacAgentMessageStore messages)
{
    public async Task<RacAgentMessage> PublishAsync(Guid correlationId,Guid rafaelTask,Guid samuelTask,Guid miguelTask,
        DateTimeOffset observedUtc,TimeSpan maximumAge,CancellationToken ct)
    {
        if(correlationId==Guid.Empty) throw new ArgumentException("Invalid advisory correlation.");
        var result=await service.ReviewAsync(rafaelTask,samuelTask,miguelTask,observedUtc,maximumAge,ct);
        var message=CarlosFinancialAdvisoryReport.CreateForPublication(correlationId,result,rafaelTask,samuelTask,miguelTask,observedUtc);
        return await messages.SaveFinancialAdvisoryAsync(message,ct);
    }
}
