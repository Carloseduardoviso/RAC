using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace RAC.Infrastructure;
public sealed class ResearchLicenseStore(IDbContextFactory<RacDbContext> factory)
{
    public async Task<int> SaveAsync(IReadOnlyList<ResearchLicenseMetadata.License> licenses,DateTimeOffset now,CancellationToken ct)
    {
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        int count=0;
        var seen=new HashSet<(string,string)>();
        foreach(var l in licenses.Take(200))
        {
            var version=l.ContentVersion??"";
            if(l.Doi.Length>255||l.Url.Length>2048||version.Length>100)continue;
            var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[]{l.Url,version}))));
            if(!seen.Add((l.Doi,hash)))continue;
            if(!await db.ResearchPublications.AnyAsync(p=>p.Doi==l.Doi,ct))continue;
            if(await db.ResearchLicenses.AnyAsync(p=>p.Doi==l.Doi&&p.EvidenceHash==hash,ct))continue;
            db.ResearchLicenses.Add(new ResearchLicenseRecord{Doi=l.Doi,Url=l.Url,ContentVersion=version,EvidenceHash=hash,ObservedUtc=now});
            count++;
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return count;
    }
}
