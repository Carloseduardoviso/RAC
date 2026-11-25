using Microsoft.EntityFrameworkCore;
using RAC.Application;
namespace RAC.Infrastructure;
public sealed record RacAnaFinancialEvidenceAuditResult(Guid MessageId,IReadOnlyList<Guid> VerifiedIds,IReadOnlyList<Guid> InvalidIds)
{
    public bool TradingAuthorized=>false;
}
public sealed class RacAnaFinancialEvidenceAudit(IDbContextFactory<RacDbContext> factory)
{
    public async Task<RacAnaFinancialEvidenceAuditResult> ReviewAsync(RacAgentMessage message,TimeSpan maximumAge,CancellationToken ct)
    {
        if(maximumAge<=TimeSpan.Zero || maximumAge>TimeSpan.FromDays(1))
            throw new ArgumentException("Invalid evidence window.");
        var report=RacAnaFinancialReportReview.Review(message);
        if(report.Status=="INVALID_FINANCIAL_REPORT") throw new ArgumentException("Invalid financial report.");
        var ids=report.EvidenceIds.ToArray();
        await using var db=await factory.CreateDbContextAsync(ct);
        var rows=await db.AgentMessages.AsNoTracking().Where(x=>ids.Contains(x.Id)).ToListAsync(ct);
        var verified=rows.Where(x=>x.Recipient=="carlos" && x.Kind=="Report" &&
            x.CorrelationId!=Guid.Empty && x.CreatedUtc<=message.CreatedUtc &&
            message.CreatedUtc-x.CreatedUtc<=maximumAge &&
            !string.IsNullOrWhiteSpace(x.Content) && x.Content.Length<=4096 &&
            (x.Sender=="rafael" || x.Sender=="samuel" || x.Sender=="miguel"))
            .Select(x=>x.Id).OrderBy(x=>x).ToArray();
        return new(message.Id,Array.AsReadOnly(verified),Array.AsReadOnly(ids.Except(verified).OrderBy(x=>x).ToArray()));
    }
}
