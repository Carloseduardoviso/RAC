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
        => (await ReviewBatchAsync([message],maximumAge,ct)).Single();
    public async Task<IReadOnlyList<RacAnaFinancialEvidenceAuditResult>> ReviewBatchAsync(
        IEnumerable<RacAgentMessage> messages,TimeSpan maximumAge,CancellationToken ct)
    {
        if(maximumAge<=TimeSpan.Zero || maximumAge>TimeSpan.FromDays(1))
            throw new ArgumentException("Invalid evidence window.");
        var batch=messages.Take(501).Select(m=>(Message:m,Report:RacAnaFinancialReportReview.Review(m))).ToArray();
        if(batch.Length>500 || batch.Any(x=>x.Report.Status=="INVALID_FINANCIAL_REPORT") ||
            batch.Select(x=>x.Message.Id).Distinct().Count()!=batch.Length)
            throw new ArgumentException("Invalid financial audit batch.");
        var ids=batch.SelectMany(x=>x.Report.EvidenceIds).Distinct().ToArray();
        var rows=new Dictionary<Guid,RAC.Domain.RacAgentMessageRecord>();
        if(ids.Length>0)
        {
            await using var db=await factory.CreateDbContextAsync(ct);
            foreach(var chunk in ids.Chunk(500))
            {
                var found=await db.AgentMessages.AsNoTracking().Where(x=>chunk.Contains(x.Id)).ToListAsync(ct);
                foreach(var row in found) rows.Add(row.Id,row);
            }
        }
        return Array.AsReadOnly(batch.Select(item=>{
            var message=item.Message;
            var verified=item.Report.EvidenceIds.Where(id=>rows.TryGetValue(id,out var x) &&
                x.Recipient=="carlos" && x.Kind=="Report" && x.CorrelationId!=Guid.Empty &&
                x.CreatedUtc<=message.CreatedUtc && message.CreatedUtc-x.CreatedUtc<=maximumAge &&
                !string.IsNullOrWhiteSpace(x.Content) && x.Content.Length<=4096 &&
                (x.Sender=="rafael" || x.Sender=="samuel" || x.Sender=="miguel"))
                .OrderBy(x=>x).ToArray();
            return new RacAnaFinancialEvidenceAuditResult(message.Id,Array.AsReadOnly(verified),
                Array.AsReadOnly(item.Report.EvidenceIds.Except(verified).OrderBy(x=>x).ToArray()));
        }).ToArray());
    }
}
