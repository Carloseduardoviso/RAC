using Microsoft.EntityFrameworkCore;
using RAC.Application;
using System.Text.Json;

namespace RAC.Infrastructure;

public sealed class RacAnaAdvisoryRecord
{
    public string Fingerprint { get; set; } = "";
    public string Status { get; set; } = "";
    public string ReasonsJson { get; set; } = "";
    public string EvidenceIdsJson { get; set; } = "";
    public DateTimeOffset CreatedUtc { get; set; }
}

public sealed class RacAnaAdvisoryStore(IDbContextFactory<RacDbContext> factory)
{
    public async Task<bool> SaveAsync(RacAnaAdvisoryResult result, DateTimeOffset now, CancellationToken ct)
    {
        var key = RacAnaAdvisoryFingerprint.Compute(result);
        await using var db = await factory.CreateDbContextAsync(ct);
        db.AnaAdvisories.Add(new RacAnaAdvisoryRecord {
            Fingerprint=key, Status=result.Status, CreatedUtc=now.ToUniversalTime(),
            ReasonsJson=JsonSerializer.Serialize(result.Reasons),
            EvidenceIdsJson=JsonSerializer.Serialize(result.EvidenceIds)
        });
        try { await db.SaveChangesAsync(ct); }
        catch(DbUpdateException ex) when(ex.InnerException is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 2601 or 2627)
        {
            if(await db.AnaAdvisories.AsNoTracking().AnyAsync(x=>x.Fingerprint==key,ct)) return false;
            throw;
        }
        return true;
    }
}
