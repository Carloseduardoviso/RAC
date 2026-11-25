using RAC.Application;
namespace RAC.Infrastructure;
public sealed class SamuelLiquidityReporter(RacAgentMessageStore messages)
{
    public RacAgentMessage Create(Guid taskId,string symbol,SamuelDepthSnapshot book,IReadOnlyList<decimal> quantities,decimal feePercent,decimal maxImpactPercent,decimal maxCostPercent,DateTimeOffset observedUtc) => SamuelLiquidityReport.Create(taskId,symbol,book,quantities,feePercent,maxImpactPercent,maxCostPercent,observedUtc);
    public Task<bool> PublishAsync(Guid taskId,string symbol,SamuelDepthSnapshot book,
        IReadOnlyList<decimal> quantities,decimal feePercent,decimal maxImpactPercent,
        decimal maxCostPercent,DateTimeOffset observedUtc,CancellationToken ct)
        =>messages.SaveAsync(SamuelLiquidityReport.Create(taskId,symbol,book,quantities,
            feePercent,maxImpactPercent,maxCostPercent,observedUtc),ct);
}
