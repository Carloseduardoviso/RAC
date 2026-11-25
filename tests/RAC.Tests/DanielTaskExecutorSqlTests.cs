using Microsoft.EntityFrameworkCore;
using RAC.Infrastructure;
using System.Net;
namespace RAC.Tests;

public sealed class DanielTaskExecutorSqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    private sealed class Handler(bool empty=false):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get,request.Method);
            var body=request.RequestUri!.AbsolutePath.EndsWith("exchangeInfo")
                ? """{"symbols":[{"symbol":"ABCUSDT","status":"TRADING","quoteAsset":"USDT","isSpotTradingAllowed":true},{"symbol":"MISSINGUSDT","status":"TRADING","quoteAsset":"USDT","isSpotTradingAllowed":true}]}"""
                : """[{"symbol":"ABCUSDT","lowPrice":"100","highPrice":"103","quoteVolume":"2000000","bidPrice":"101","askPrice":"101.1","closeTime":TIME}]"""
                    .Replace("TIME",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
            if(empty && !request.RequestUri!.AbsolutePath.EndsWith("exchangeInfo")) body="[]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body)});
        }
    }
    [Theory] [InlineData(false)] [InlineData(true)] public async Task ExecutesResearchAndPublishesReport(bool empty)
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_DANIEL_EXEC_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;Encrypt=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var factory=new Factory(options);
            var tasks=new RacAgentTaskStore(factory);
            var task=await tasks.EnqueueAsync("rebeca","daniel","MARKET_DISCOVERY",default);
            using var http=new HttpClient(new Handler(empty));
            var executor=new DanielTaskExecutor(factory,new DanielBinancePublicClient(http),tasks,
                new DanielDiscoveryStore(factory),new DanielDiscoveryReporter(factory));
            Assert.True(await executor.ExecuteNextAsync(default));
            Assert.False(await executor.ExecuteNextAsync(default));
            await using var check=new RacDbContext(options);
            Assert.Equal("Completed",(await check.AgentTasks.SingleAsync()).Status);
            Assert.Equal(empty?0:1,(await check.DanielDiscoveries.SingleAsync()).CandidateCount);
            var snapshot=(await check.DanielDiscoveries.SingleAsync()).ResearchSnapshotJson!;
            Assert.Contains("DANIEL_COVERAGE_V1",snapshot);
            Assert.Contains("MISSINGUSDT",snapshot);
            Assert.False(await check.TestnetOrders.AnyAsync());
            Assert.Equal(task.Id,(await check.AgentMessages.SingleAsync()).CorrelationId);
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
    [Fact]
    public async Task LivePublicDiscoveryHomologation()
    {
        if(!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("RAC_DANIEL_LIVE_PROBE")!="1") return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_DANIEL_EXEC_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;Encrypt=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var factory=new Factory(options);
            var tasks=new RacAgentTaskStore(factory);
            var task=await tasks.EnqueueAsync("rebeca","daniel","MARKET_DISCOVERY",default);
            using var http=new HttpClient(new HttpClientHandler { AllowAutoRedirect=false }) { MaxResponseContentBufferSize=32*1024*1024 };
            var executor=new DanielTaskExecutor(factory,new DanielBinancePublicClient(http),tasks,
                new DanielDiscoveryStore(factory),new DanielDiscoveryReporter(factory));
            Assert.True(await executor.ExecuteNextAsync(default));
            Assert.False(await executor.ExecuteNextAsync(default));
            await using var check=new RacDbContext(options);
            Assert.Equal("Completed",(await check.AgentTasks.SingleAsync()).Status);
            var discovery=await check.DanielDiscoveries.SingleAsync(); Assert.InRange(discovery.CandidateCount,0,20); Console.WriteLine("LIVE_CANDIDATES="+discovery.CandidateCount);
            Assert.Equal(task.Id,(await check.AgentMessages.SingleAsync()).CorrelationId);
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
    [Fact]
    public async Task RecoversPersistedDiscoveryAfterExpiredLease()
    {
        if(!OperatingSystem.IsWindows()) return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(
            @"Server=(localdb)\MSSQLLocalDB;Database=RAC_DANIEL_EXEC_TEST_"+Guid.NewGuid().ToString("N")+
            ";Integrated Security=True;Encrypt=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options)) await db.Database.MigrateAsync();
            var factory=new Factory(options);
            var tasks=new RacAgentTaskStore(factory);
            var task=await tasks.EnqueueAsync("rebeca","daniel","MARKET_DISCOVERY",default);
            using var http=new HttpClient(new Handler());
            var executor=new DanielTaskExecutor(factory,new DanielBinancePublicClient(http),tasks,
                new DanielDiscoveryStore(factory),new DanielDiscoveryReporter(factory));
                        // Simulate interruption after persistence but before report publication.
            var claimed=(await tasks.ClaimAsync("daniel",default))!;
            var now=DateTimeOffset.UtcNow;
            await new DanielDiscoveryStore(factory).SaveAsync(task.Id,claimed.LeaseId!.Value,
                new[]{new RAC.Application.DanielMarketObservation("ABCUSDT",true,2000000m,100m,100.1m,3m,now)},now,default);
            await using(var interrupted=new RacDbContext(options))
            {
                var row=await interrupted.AgentTasks.SingleAsync();
                row.UpdatedUtc=now.AddMinutes(-10);
                row.LeaseUntilUtc=now.AddMinutes(-5);
                await interrupted.SaveChangesAsync();
            }
            Assert.Equal(1,await tasks.RecoverExpiredAsync(default));
            Assert.True(await executor.ExecuteNextAsync(default));
            Assert.False(await executor.ExecuteNextAsync(default));
            await using var check=new RacDbContext(options);
            Assert.Equal("Completed",(await check.AgentTasks.SingleAsync()).Status);
            Assert.Equal(1,(await check.DanielDiscoveries.SingleAsync()).CandidateCount);
            Assert.Equal(task.Id,(await check.AgentMessages.SingleAsync()).CorrelationId);
        }
        finally
        {
            await using var db=new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }

}
