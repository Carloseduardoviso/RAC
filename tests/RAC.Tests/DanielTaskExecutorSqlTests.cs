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
    private sealed class Handler(bool empty=false,bool complete=false):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get,request.Method);
            var body=request.RequestUri!.AbsolutePath.EndsWith("exchangeInfo")
                ? """{"symbols":[{"symbol":"ABCUSDT","status":"TRADING","quoteAsset":"USDT","isSpotTradingAllowed":true},{"symbol":"MISSINGUSDT","status":"TRADING","quoteAsset":"USDT","isSpotTradingAllowed":true}]}"""
                : """[{"symbol":"ABCUSDT","lowPrice":"100","highPrice":"103","quoteVolume":"2000000","bidPrice":"101","askPrice":"101.1","closeTime":TIME}]"""
                    .Replace("TIME",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
            if(complete && request.RequestUri!.AbsolutePath.EndsWith("exchangeInfo")) body="""{"symbols":[{"symbol":"ABCUSDT","status":"TRADING","quoteAsset":"USDT","isSpotTradingAllowed":true}]}""";
            if(empty && !request.RequestUri!.AbsolutePath.EndsWith("exchangeInfo")) body="[]";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body)});
        }
    }
    [Theory] [InlineData(false,false)] [InlineData(true,false)] [InlineData(false,true)] public async Task ExecutesResearchAndPublishesReport(bool empty,bool complete)
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
            using var http=new HttpClient(new Handler(empty,complete));
            var executor=new DanielTaskExecutor(factory,new DanielBinancePublicClient(http),tasks,
                new DanielDiscoveryStore(factory),new DanielDiscoveryReporter(factory));
            Assert.True(await executor.ExecuteNextAsync(default));
            Assert.False(await executor.ExecuteNextAsync(default));
            await using var check=new RacDbContext(options);
            Assert.Equal("Completed",(await check.AgentTasks.SingleAsync(x=>x.Id==task.Id)).Status);
            Assert.Equal(empty?0:1,(await check.DanielDiscoveries.SingleAsync()).CandidateCount);
            var snapshot=(await check.DanielDiscoveries.SingleAsync()).ResearchSnapshotJson!;
            Assert.Contains("DANIEL_COVERAGE_V1",snapshot);
            if(!complete) Assert.Contains("MISSINGUSDT",snapshot);
            var report=await check.AgentMessages.SingleAsync();
            using var json=System.Text.Json.JsonDocument.Parse(report.Content);
            Assert.Equal(empty?"NO_VALID_OBSERVATIONS":complete?"RESEARCH_CANDIDATES_AVAILABLE":"PARTIAL_COVERAGE",json.RootElement.GetProperty("CoverageStatus").GetString());
            Assert.False(json.RootElement.GetProperty("TradingAuthorized").GetBoolean());
            var replay=await new DanielDiscoveryReporter(factory).CreateAsync(task.Id,default);
            Assert.Equal(report.Id,replay.Id);
            Assert.Equal(report.Content,replay.Content);
            var linked=await check.AgentTasks.Where(x=>x.Specialist=="gabriel").ToListAsync();
            if(complete)
            {
                var next=Assert.Single(linked);
                Assert.Equal("Pending",next.Status);
                Assert.Equal("rebeca",next.Leader);
                Assert.Equal("QUANTITATIVE_RESEARCH:"+task.Id.ToString("N"),next.Description);
            }
            else Assert.Empty(linked);
            var reader=new GabrielDiscoveryEvidenceReader(new RebecaMarketEvidenceReader(new RacAgentMessageStore(factory)));
            if(complete)
            {
                var evidence=await reader.ReadAsync(task.Id,DateTimeOffset.UtcNow,default);
                Assert.Equal(report.Id,evidence.MessageId);
                Assert.Equal("ABCUSDT",Assert.Single(evidence.Candidates).Symbol);
                Assert.Equal("MATRIX_EVIDENCE_MISSING",Assert.Single(GabrielDiscoveryResearch.Assess(evidence,[])).Status);
                Assert.False(evidence.TradingAuthorized);
                await Assert.ThrowsAsync<InvalidOperationException>(()=>reader.ReadAsync(task.Id,DateTimeOffset.UtcNow.AddMinutes(16),default));
            }
            else await Assert.ThrowsAsync<InvalidOperationException>(()=>reader.ReadAsync(task.Id,DateTimeOffset.UtcNow,default));
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
            Assert.Equal("Completed",(await check.AgentTasks.SingleAsync(x=>x.Id==task.Id)).Status);
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
            Assert.Equal("Completed",(await check.AgentTasks.SingleAsync(x=>x.Id==task.Id)).Status);
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
