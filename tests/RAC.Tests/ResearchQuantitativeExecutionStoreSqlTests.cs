using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class ResearchQuantitativeExecutionStoreSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task StoresBothOutcomesAndRejectsConflictsUnderConcurrency()
    {
        if(!OperatingSystem.IsWindows())return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=RAC_QUANT_TEST_"+Guid.NewGuid().ToString("N")+";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options))await db.Database.MigrateAsync();
            var store=new ResearchQuantitativeExecutionStore(new Factory(options));
            var row=ResearchExperimentPlan.Build(["ABCUSDT"])[0];
            var month=new DateOnly(2026,5,1);
            var now=new DateTimeOffset(2026,11,25,0,0,0,TimeSpan.Zero);
            var hash=new string('A',64);
            var result=new ResearchExperimentReplay.Result(2,1,5,3,2,1,false);
            Task<bool> Save(ResearchExperimentReplay.Result r,DateOnly m)=>store.SaveAsync(row,hash,m,"REPLAY_V1",r,now,default);
            var saved=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>Save(result,month)));
            Assert.Single(saved,x=>x);
            Assert.False(await Save(result with {NetPct=2.00m},month));
            await Assert.ThrowsAsync<InvalidOperationException>(()=>Save(result with {NetPct=3},month));
            Assert.True(await Save(result with {NetPct=-2},month.AddMonths(1)));
            await Assert.ThrowsAsync<ArgumentException>(()=>Save(result,new DateOnly(2026,11,1)));
            await Assert.ThrowsAsync<ArgumentException>(()=>Save(result with {Wins=6},month));
            await using var check=new RacDbContext(options);
            Assert.Equal(2,await check.ResearchQuantitativeExecutions.CountAsync());
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally {await using var db=new RacDbContext(options);await db.Database.EnsureDeletedAsync();}
    }
}
