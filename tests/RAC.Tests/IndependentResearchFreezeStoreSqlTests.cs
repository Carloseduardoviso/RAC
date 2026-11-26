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
            await using(var db=factory.CreateDbContext())
            {
                var stored=await db.ResearchExecutionManifests.SingleAsync();
                var original=stored.EvidenceJson;
                var node=System.Text.Json.Nodes.JsonNode.Parse(original)!;
                var hashes=node["Results"]!.AsObject();
                var key=hashes.First().Key;
                var value=hashes[key]!.GetValue<string>();
                hashes.Remove(key);
                hashes.Add(new string('F',64),value);
                stored.EvidenceJson=node.ToJsonString();
                await db.SaveChangesAsync();
                var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>store.SaveAsync(plan[1],manifest.Id,default));
                Assert.Contains("Invalid manifest identity",error.Message);
                stored.EvidenceJson=original;
                await db.SaveChangesAsync();
            }
            clock.Now=IndependentResearchFreeze.ValidationStart;
            await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveAsync(plan[1],manifest.Id,default));
            await using var check=factory.CreateDbContext();
            var frozen=await check.IndependentResearchFreezes.SingleAsync();
            Assert.Equal(id,frozen.FreezeId);
            Assert.Equal(now,frozen.FrozenUtc);
            var update=await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(()=>check.Database.ExecuteSqlRawAsync("UPDATE rac.IndependentResearchFreezes SET Symbol = 'CHANGED'"));
            Assert.Equal(51001,update.Number);
            var delete=await Assert.ThrowsAsync<Microsoft.Data.SqlClient.SqlException>(()=>check.Database.ExecuteSqlRawAsync("DELETE FROM rac.IndependentResearchFreezes"));
            Assert.Equal(51001,delete.Number);
            await check.Entry(frozen).ReloadAsync();
            Assert.Equal(plan[0].Symbol,frozen.Symbol);
            Assert.Equal(id,frozen.FreezeId);
            Assert.Equal(now,frozen.FrozenUtc);
            Assert.Equal(1,await check.IndependentResearchFreezes.CountAsync());
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally
        {
            await using var db=factory.CreateDbContext();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
