using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class ResearchExecutionManifestStoreSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task RequiresPersistedExecutionsAndDoesNotDuplicateManifest()
    {
        if(!OperatingSystem.IsWindows())return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=RAC_MANIFEST_TEST_"+Guid.NewGuid().ToString("N")+";Integrated Security=True;TrustServerCertificate=True").Options;
        var factory=new Factory(options);
        try
        {
            await using(var db=factory.CreateDbContext())await db.Database.MigrateAsync();
            var symbols=new[]{"ABCUSDT"};
            var engine=ResearchEngineCatalog.Current.Id;
            var evidence=ResearchExperimentPlan.Build(symbols).SelectMany(row=>Enumerable.Range(5,6).Select(m=>new ResearchExecutionManifest.Evidence(row,new DateOnly(2026,m,1),new string('A',64)))).ToArray();
            var store=new ResearchExecutionManifestStore(factory);
            var executions=new ResearchQuantitativeExecutionStore(factory);
            var now=new DateTimeOffset(2026,11,25,0,0,0,TimeSpan.Zero);
            var result=new ResearchExperimentReplay.Result(0,0,0,0,null,0,false);
            foreach(var item in evidence.SkipLast(1))
                await executions.SaveAsync(item.Row,item.SourceSha256,item.Month,engine,result,now,default);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>store.SaveAsync(symbols,engine,evidence,now,default));
            await using(var db=factory.CreateDbContext())Assert.False(await db.ResearchExecutionManifests.AnyAsync());
            var last=evidence.Last();
            await executions.SaveAsync(last.Row,last.SourceSha256,last.Month,engine,result,now,default);
            Assert.True(await store.SaveAsync(symbols,engine,evidence,now,default));
            Assert.False(await store.SaveAsync(symbols,engine,evidence.Reverse().ToArray(),now,default));
            await using var check=factory.CreateDbContext();
            var manifest=await check.ResearchExecutionManifests.SingleAsync();
            Assert.Equal(144,manifest.Executions);
            Assert.Equal(ResearchExecutionManifest.Create(symbols,engine,evidence).Id,manifest.ManifestId);
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally
        {
            await using var db=factory.CreateDbContext();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
