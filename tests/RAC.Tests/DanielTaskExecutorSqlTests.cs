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
    private sealed class Handler:HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get,request.Method);
            var body=request.RequestUri!.AbsolutePath.EndsWith("exchangeInfo")
                ? """{"symbols":[{"symbol":"ABCUSDT","status":"TRADING","quoteAsset":"USDT","isSpotTradingAllowed":true}]}"""
                : """[{"symbol":"ABCUSDT","lowPrice":"100","highPrice":"103","quoteVolume":"2000000","bidPrice":"101","askPrice":"101.1","closeTime":TIME}]"""
                    .Replace("TIME",DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body)});
        }
    }
    [Fact]
    public async Task ExecutesResearchAndPublishesReport()
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
