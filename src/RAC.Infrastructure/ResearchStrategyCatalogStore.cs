using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace RAC.Infrastructure;
public sealed class ResearchStrategyCatalogStore(IDbContextFactory<RacDbContext> factory)
{
    public async Task<bool> SaveAsync(string json,DateTimeOffset now,CancellationToken ct)
    {
        if(json.Length>1000000)throw new InvalidDataException("Catalog too large");
        using var doc=JsonDocument.Parse(json);
        var m=doc.RootElement;
        var source=m.GetProperty("SourceSha256").GetString()??"";
        var version=m.GetProperty("Version").GetString()??"";
        if(source.Length!=64||!source.All(Uri.IsHexDigit)||version.Length is <1 or >64||m.GetProperty("SchemaVersion").GetInt32()!=1||m.GetProperty("Mode").GetString()!="RESEARCH_ONLY"||m.GetProperty("TradingAuthorized").GetBoolean()||m.GetProperty("IndependentValidation").GetBoolean())throw new InvalidDataException("Unsafe catalog");
        var entries=m.GetProperty("Entries").EnumerateArray().ToArray();
        if(entries.Length!=12||entries.Select(x=>x.GetProperty("Evidence").GetProperty("Symbol").GetString()).Distinct().Count()!=12||entries.Any(x=>x.GetProperty("TradingAuthorized").GetBoolean()||x.GetProperty("Evidence").GetProperty("Decision").GetProperty("TradingAuthorized").GetBoolean()))throw new InvalidDataException("Invalid entries");
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        if(await db.ResearchStrategyCatalogs.AnyAsync(x=>x.ContentSha256==hash,ct))return false;
        db.ResearchStrategyCatalogs.Add(new ResearchStrategyCatalogRecord{ContentSha256=hash,SourceSha256=source,Version=version,CatalogJson=json,ArchivedUtc=now});
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return true;
    }
}
