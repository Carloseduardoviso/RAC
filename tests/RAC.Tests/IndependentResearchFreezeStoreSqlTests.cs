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
    private sealed class CommitFault:Microsoft.EntityFrameworkCore.Diagnostics.DbTransactionInterceptor
    {
        public bool Armed {get;set;}
        public override Task TransactionCommittedAsync(System.Data.Common.DbTransaction transaction,Microsoft.EntityFrameworkCore.Diagnostics.TransactionEndEventData eventData,CancellationToken cancellationToken=default)
        {
            if(Armed)
            {
                Armed=false;
                throw new InvalidOperationException("Simulated lost commit acknowledgement.");
            }
            return Task.CompletedTask;
        }
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
            var stress=plan.First(x=>x.Parameters.SlippagePctPerSide==.20m && x.Parameters!= (plan[0].Parameters with {SlippagePctPerSide=.20m}) && x.Parameters!=(plan[1].Parameters with {SlippagePctPerSide=.20m}));
            var normal=stress with {Parameters=stress.Parameters with {SlippagePctPerSide=.05m}};
            var normalId=ResearchExperimentIdentity.Create(normal);
            await using(var db=factory.CreateDbContext())
            {
                var record=await db.ResearchQuantitativeExecutions.FirstAsync(x=>x.ConfigurationId==normalId);
                record.ResultJson=System.Text.Json.JsonSerializer.Serialize(result with {NetPct=1});
                await db.SaveChangesAsync();
                await Assert.ThrowsAsync<InvalidOperationException>(()=>store.SavePairAsync(stress,manifest.Id,default));
                Assert.False(await db.IndependentResearchFreezes.AnyAsync());
                record.ResultJson=System.Text.Json.JsonSerializer.Serialize(result);
                await db.SaveChangesAsync();
            }
            var pairs=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>store.SavePairAsync(stress,manifest.Id,default)));
            Assert.All(pairs,pair=>Assert.Equal(pairs[0].ToArray(),pair.ToArray()));
            Assert.Equal(2,pairs[0].Distinct().Count());
            await using(var db=factory.CreateDbContext())
            {
                Assert.Equal(2,await db.IndependentResearchFreezes.CountAsync());
                Assert.All(await db.IndependentResearchFreezes.ToListAsync(),x=>Assert.Equal(now,x.FrozenUtc));
            }
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
            // Simulate a stopped batch after its first committed configuration.
            using(var stopped=new CancellationTokenSource())
            {
                stopped.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>store.SaveAsync(plan[1],manifest.Id,stopped.Token));
            }
            // Restore the deliberately corrupted historical result before resuming.
            await using(var db=factory.CreateDbContext())
            {
                var configuration=ResearchExperimentIdentity.Create(plan[0]);
                var records=await db.ResearchQuantitativeExecutions.Where(x=>x.ConfigurationId==configuration).ToListAsync();
                foreach(var record in records) record.ResultJson=System.Text.Json.JsonSerializer.Serialize(result);
                await db.SaveChangesAsync();
            }
            Assert.Equal(id,await store.SaveAsync(plan[0],manifest.Id,default));
            var resumed=await store.SaveAsync(plan[1],manifest.Id,default);
            Assert.NotEqual(id,resumed);
            Assert.Equal(resumed,await store.SaveAsync(plan[1],manifest.Id,default));
            // Recover a legacy single configuration without rewriting its timestamp or identity.
            var legacyStress=plan.First(x=>x.Parameters.SlippagePctPerSide==.20m && x!=stress && x.Parameters!=(plan[0].Parameters with {SlippagePctPerSide=.20m}) && x.Parameters!=(plan[1].Parameters with {SlippagePctPerSide=.20m}));
            var legacyNormal=legacyStress with {Parameters=legacyStress.Parameters with {SlippagePctPerSide=.05m}};
            var legacyId=await store.SaveAsync(legacyStress,manifest.Id,default);
            var legacyTime=clock.Now;
            clock.Now=legacyTime.AddMinutes(1);
            var recovered=await store.SavePairAsync(legacyStress,manifest.Id,default);
            Assert.Equal(legacyId,recovered[0]);
            Assert.Equal(recovered.ToArray(),(await store.SavePairAsync(legacyStress,manifest.Id,default)).ToArray());
            await using(var db=factory.CreateDbContext())
            {
                var old=await db.IndependentResearchFreezes.SingleAsync(x=>x.FreezeId==legacyId);
                var companion=await db.IndependentResearchFreezes.SingleAsync(x=>x.FreezeId==recovered[1]);
                Assert.Equal(legacyTime,old.FrozenUtc);
                Assert.Equal(clock.Now,companion.FrozenUtc);
                Assert.Equal(ResearchExperimentIdentity.Create(legacyNormal),companion.ConfigurationId);
                Assert.Equal(old.ManifestId,companion.ManifestId);
                Assert.Equal(6,await db.IndependentResearchFreezes.CountAsync());
            }
            // Force a database-side insert failure after evidence validation.
            var failingStress=plan.First(x=>x.Parameters.SlippagePctPerSide==.20m && x!=stress && x!=legacyStress && x.Parameters!=(plan[0].Parameters with {SlippagePctPerSide=.20m}) && x.Parameters!=(plan[1].Parameters with {SlippagePctPerSide=.20m}));
            var failingNormal=failingStress with {Parameters=failingStress.Parameters with {SlippagePctPerSide=.05m}};
            var failingId=ResearchExperimentIdentity.Create(failingNormal);
            Assert.Matches("^[0-9A-F]{64}$",failingId);
            await using(var db=factory.CreateDbContext())
            {
                #pragma warning disable EF1003 // Test-only DDL; configuration digest validated as uppercase hexadecimal.
                await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER rac.TR_TestFreezeInsertFailure ON rac.IndependentResearchFreezes AFTER INSERT AS BEGIN SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM inserted WHERE ConfigurationId = '"+failingId+"') THROW 51002, 'Injected insert failure.', 1; END");
                #pragma warning restore EF1003
                try
                {
                    var failure=await Assert.ThrowsAsync<DbUpdateException>(()=>store.SavePairAsync(failingStress,manifest.Id,default));
                    Assert.Equal(51002,Assert.IsType<Microsoft.Data.SqlClient.SqlException>(failure.InnerException).Number);
                    Assert.Equal(6,await db.IndependentResearchFreezes.CountAsync());
                }
                finally
                {
                    await db.Database.ExecuteSqlRawAsync("DROP TRIGGER rac.TR_TestFreezeInsertFailure");
                }
            }
            // Retry after the database failure has been removed.
            var retried=await store.SavePairAsync(failingStress,manifest.Id,default);
            Assert.Equal(2,retried.Distinct().Count());
            Assert.Equal(retried.ToArray(),(await store.SavePairAsync(failingStress,manifest.Id,default)).ToArray());
            await using(var db=factory.CreateDbContext())
            {
                Assert.Equal(8,await db.IndependentResearchFreezes.CountAsync());
                Assert.Equal(2,await db.IndependentResearchFreezes.CountAsync(x=>retried.Contains(x.FreezeId)));
            }
            // Simulate loss of acknowledgement after the database commits.
            var fault=new CommitFault();
            var faultOptions=new DbContextOptionsBuilder<RacDbContext>(options).AddInterceptors(fault).Options;
            var faultStore=new IndependentResearchFreezeStore(new Factory(faultOptions),clock);
            var committedStress=plan.First(x=>x.Parameters.SlippagePctPerSide==.20m && x!=stress && x!=legacyStress && x!=failingStress && x.Parameters!=(plan[0].Parameters with {SlippagePctPerSide=.20m}) && x.Parameters!=(plan[1].Parameters with {SlippagePctPerSide=.20m}));
            fault.Armed=true;
            var lost=await Assert.ThrowsAsync<InvalidOperationException>(()=>faultStore.SavePairAsync(committedStress,manifest.Id,default));
            Assert.Equal("Simulated lost commit acknowledgement.",lost.Message);
            var recoveredCommit=await store.SavePairAsync(committedStress,manifest.Id,default);
            Assert.Equal(2,recoveredCommit.Distinct().Count());
            await using(var db=factory.CreateDbContext())
            {
                Assert.Equal(10,await db.IndependentResearchFreezes.CountAsync());
                Assert.Equal(2,await db.IndependentResearchFreezes.CountAsync(x=>recoveredCommit.Contains(x.FreezeId)));
            }
            clock.Now=IndependentResearchFreeze.ValidationStart;
            await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveAsync(plan[2],manifest.Id,default));
            await using var check=factory.CreateDbContext();
            var frozen=await check.IndependentResearchFreezes.SingleAsync(x=>x.FreezeId==id);
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
            Assert.Equal(10,await check.IndependentResearchFreezes.CountAsync());
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally
        {
            await using var db=factory.CreateDbContext();
            await db.Database.EnsureDeletedAsync();
        }
    }
}
