using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class IndependentResearchEvidenceSchemaTests
{
    [Fact]
    public async Task MigrationEnforcesForeignKeyUniquenessAndAppendOnly()
    {
        if(!OperatingSystem.IsWindows())return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=RAC_EVIDENCE_TEST_"+Guid.NewGuid().ToString("N")+";Integrated Security=True;TrustServerCertificate=True").Options;
        await using var db=new RacDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var now=new DateTimeOffset(2026,11,25,0,0,0,TimeSpan.Zero);
            var manifest=new string('A',64);var freeze=new string('B',64);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO rac.ResearchExecutionManifests (ManifestId,EngineVersion,Assets,Executions,EvidenceJson,RecordedUtc) VALUES ({manifest},'REPLAY_V1',1,144,'{{}}',{now})");
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO rac.IndependentResearchFreezes (FreezeId,ConfigurationId,ManifestId,EngineVersion,Symbol,ParametersJson,FrozenUtc) VALUES ({freeze},{new string('C',64)},{manifest},'REPLAY_V1','ABCUSDT','{{}}',{now})");
            IndependentResearchEvidenceRecord Row(string id,string parent)=>new(){ExecutionId=id,FreezeId=parent,SourceSha256=new string('D',64),Digest=new string('E',64),Month=new DateOnly(2026,12,1),EvidenceJson="{}",RecordedUtc=now.AddMonths(2)};
            var first=Row(new string('F',64),freeze);
            db.IndependentResearchEvidence.Add(first);await db.SaveChangesAsync();
            db.IndependentResearchEvidence.Add(Row(new string('1',64),freeze));
            await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());db.ChangeTracker.Clear();
            db.IndependentResearchEvidence.Add(Row(new string('2',64),new string('3',64)));
            await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(()=>db.Database.ExecuteSqlRawAsync("UPDATE rac.IndependentResearchEvidence SET EvidenceJson='changed'"));
            await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(()=>db.Database.ExecuteSqlRawAsync("DELETE FROM rac.IndependentResearchEvidence"));
            Assert.Equal("{}",(await db.IndependentResearchEvidence.SingleAsync()).EvidenceJson);
        }
        finally{await db.Database.EnsureDeletedAsync();}
    }
}
