using Microsoft.EntityFrameworkCore;
using RAC.Application;
namespace RAC.Infrastructure;

// Read-only provenance check; never grants trading permission.
public sealed class RacAnaMarketEvidenceAudit(IDbContextFactory<RacDbContext> factory)
{
    public async Task<bool> VerifyAsync(RacAgentMessage message, RacAnaMarketReportResult report, TimeSpan maxAge, CancellationToken ct)
    {
        if(maxAge<=TimeSpan.Zero || maxAge>TimeSpan.FromDays(1) || report.MessageId!=message.Id || report.Status=="INVALID_REPORT") return false;
        await using var db=await factory.CreateDbContextAsync(ct);
        var ids=report.EvidenceIds.ToArray();
        var rows=await db.AgentMessages.AsNoTracking().Where(x=>ids.Contains(x.Id)).ToListAsync(ct);
        if(rows.Count!=ids.Length || rows.Any(x=>x.Recipient!="rebeca" || x.Kind!="Report" || x.CorrelationId==Guid.Empty || (x.Sender!="daniel" && x.Sender!="gabriel") || x.CreatedUtc>message.CreatedUtc || message.CreatedUtc-x.CreatedUtc>maxAge || string.IsNullOrWhiteSpace(x.Content) || x.Content.Length>4096)) return false;
        var d=rows.Where(x=>x.Sender=="daniel").Select(x=>x.CorrelationId).Distinct().ToArray();
        var g=rows.Where(x=>x.Sender=="gabriel").Select(x=>x.CorrelationId).Distinct().ToArray();
        if(d.Length!=1 || g.Length!=1 || d[0]==g[0]) return false;
        if(RebecaMarketAdvisoryPublisher.CreateCorrelationId(d[0],g[0])!=message.CorrelationId) return false;
        var discovery=RebecaMarketEvidenceReader.Select("daniel",d[0],rows,message.CreatedUtc,maxAge);
        var quantitative=RebecaMarketEvidenceReader.Select("gabriel",g[0],rows,message.CreatedUtc,maxAge);
        if(!VerifyDynamicEvidence(discovery,quantitative)) return false;
        var rebuilt=RebecaMarketAdvisoryReport.Create(message.CorrelationId,new(report.Status,report.EvidenceIds,report.Reasons,[]),message.CreatedUtc);
        return rebuilt.Id==message.Id && rebuilt.Content==message.Content;
    }

    public static bool VerifyDynamicEvidence(IReadOnlyList<RebecaMarketEvidence> discovery,IReadOnlyList<RebecaMarketEvidence> quantitative)
    {
        foreach(var evidence in quantitative)
        {
            try
            {
                using var doc=System.Text.Json.JsonDocument.Parse(evidence.Content);
                if(doc.RootElement.TryGetProperty("Schema",out _) &&
                    (quantitative.Count!=1 || RebecaDynamicEvidenceLink.Review(evidence,discovery).Status=="INVALID_REPORT"))
                    return false;
            }
            catch(System.Text.Json.JsonException){return false;}
            catch(InvalidOperationException){return false;}
        }
        return true;
    }
}
