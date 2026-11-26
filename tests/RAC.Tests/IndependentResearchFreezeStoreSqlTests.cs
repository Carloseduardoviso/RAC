using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class IndependentResearchFreezeStoreSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    private sealed class Clock(DateTimeOffset now):TimeProvider
    {
        public DateTimeOffset Now=now;
        public override DateTimeOffset GetUtcNow()=>Now;
    }
    [Fact]
    public async Task FreezeRequiresVerifiedEvidenceAndRejectsLateRegistration()
    {
        if(!OperatingSystem.IsWindows())return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=RAC_FREEZE_TEST_"+Guid.NewGuid().ToString("N")+";Integrated Security=True;TrustServerCertificate=True").Options;
        var factory=new Factory(options);
        try
        {
            await using(var db=factory.CreateDbContext())await db.Database.MigrateAsync();
            var symbols=new[]{"ABCUSDT"};
            var plan=ResearchExperimentPlan.Build(symbols);
            var engine=ResearchEngineCatalog.Current.Id;
            var now=new DateTimeOffset(2026,11,25,0,0,0,TimeSpan.Zero);
            var clock=new Clock(now);
            var store=new IndependentResearchFreezeStore(factory,clock);
            var evidence=plan.SelectMany(row=>Enumerable.Range(5,6).Select(m=>new ResearchExecutionManifest.Evidence(row,new DateOnly(2026,m,1),new string('A',64)))).ToArray();
            var manifest=ResearchExecutionManifest.Create(symbols,engine,evidence);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>store.SaveAsync(plan[0],manifest.Id,default));
            var executions=new ResearchQuantitativeExecutionStore(factory);
            var result=new ResearchExperimentReplay.Result(0,0,0,0,null,0,false);
            foreach(var item in evidence)await executions.SaveAsync(item.Row,item.SourceSha256,item.Month,engine,result,now,default);
            await new ResearchExecutionManifestStore(factory).SaveAsync(symbols,engine,evidence,now,default);
            var concurrent=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>store.SaveAsync(plan[0],manifest.Id,default)));
            var id=Assert.Single(concurrent.Distinct());
            clock.Now=now.AddHours(1);
            Assert.Equal(id,await store.SaveAsync(plan[0],manifest.Id,default));
            await using(var db=factory.CreateDbContext())
            {
                var configuration=ResearchExperimentIdentity.Create(plan[0]);
                var record=await db.ResearchQuantitativeExecutions.FirstAsync(x=>x.ConfigurationId==configuration);
                record.ResultJson=System.Text.Json.JsonSerializer.Serialize(result with {NetPct=1});
                await db.SaveChangesAsync();
                await Assert.ThrowsAsync<InvalidOperationException>(()=>store.SaveAsync(plan[0],manifest.Id,default));
            }
            clock.Now=IndependentResearchFreeze.ValidationStart;
            await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveAsync(plan[1],manifest.Id,default));
            await using var check=factory.CreateDbContext();
            var frozen=await check.IndependentResearchFreezes.SingleAsync();
            Assert.Equal(id,frozen.FreezeId);
            Assert.Equal(now,frozen.FrozenUtc);
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally
        {
            await using var db=factory.CreateDbContext();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
