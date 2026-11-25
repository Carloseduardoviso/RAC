using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Infrastructure;
using RAC.Api;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
namespace RAC.Tests;

public sealed class GabrielLiveHistoricalTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    [Fact]
    public async Task ArchivedMatrixWorkerHomologation()
    {
        if(!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("RAC_GABRIEL_LIVE_PROBE")!="1") return;
        var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RAC","research","monthly","research-experiments-mvp16314239.json");
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_GABRIEL_LIVE_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;Encrypt=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var factory=new Factory(options);
            var tasks=new RacAgentTaskStore(factory);
            var messages=new RacAgentMessageStore(factory);
            Assert.Equal(1,await new RacSpecialistDispatcher(factory,"gabriel").DispatchAsync(DateTimeOffset.UtcNow,default));
            var executor=new GabrielTaskExecutor(tasks,messages,new GabrielResearchReporter(messages),ct=>File.ReadAllTextAsync(path,ct));
            using var worker=new GabrielResearchWorker(executor,NullLogger<GabrielResearchWorker>.Instance);
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await worker.StartAsync(timeout.Token);
            try
            {
                while(true)
                {
                    await using var db=new RacDbContext(options);
                    if(await db.AgentTasks.AnyAsync(x=>x.Status=="Completed",timeout.Token)) break;
                    await Task.Delay(200,timeout.Token);
                }
            }
            finally { await worker.StopAsync(CancellationToken.None); }
            var report=Assert.Single(await messages.ReadInboxAsync("rebeca",null,10,default));
            Assert.Equal("gabriel",report.Sender);
            using var doc=JsonDocument.Parse(report.Content);
            Assert.Equal(288,doc.RootElement.GetProperty("Configurations").GetInt32());
            Assert.Equal(144,doc.RootElement.GetProperty("StressRanked").GetInt32());
            Assert.False(doc.RootElement.GetProperty("TradingAuthorized").GetBoolean());
            Console.WriteLine("GABRIEL_HOMOLOGATED "+report.Content);
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
