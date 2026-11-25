using RAC.Application;
using System.Text.Json;
namespace RAC.Infrastructure;
public sealed record RacAnaFinancialCapacityRecoveryResult(bool Verified,IReadOnlyList<Guid> EvidenceIds)
{
    public bool TradingAuthorized=>false;
}
public sealed class RacAnaFinancialCapacityRecovery(CarlosFinancialEvidenceService service)
{
    public async Task<RacAnaFinancialCapacityRecoveryResult> ReviewAsync(RacAgentMessage message,TimeSpan maximumAge,CancellationToken ct)
    {
        var parsed=RacAnaFinancialReportReview.Review(message);
        if(parsed.Status!="CRITICAL_REVIEW" || !parsed.Reasons.Contains("CAPACITY_EXCEEDED") ||
            maximumAge<=TimeSpan.Zero || maximumAge>TimeSpan.FromDays(1))
            throw new ArgumentException("Invalid capacity recovery request.");
        using var doc=JsonDocument.Parse(message.Content);
        var root=doc.RootElement;
        var r=root.GetProperty("RafaelTask").GetGuid();
        var s=root.GetProperty("SamuelTask").GetGuid();
        var m=root.GetProperty("MiguelTask").GetGuid();
        var result=await service.ReviewAsync(r,s,m,message.CreatedUtc,maximumAge,ct);
        var rebuilt=CarlosFinancialAdvisoryReport.CreateForPublication(message.CorrelationId,result,r,s,m,message.CreatedUtc);
        var verified=rebuilt.Id==message.Id && rebuilt.Content==message.Content && rebuilt.Kind==message.Kind;
        return new(verified,verified?result.EvidenceIds:Array.Empty<Guid>());
    }
}
