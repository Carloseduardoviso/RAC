using Microsoft.EntityFrameworkCore;
using RAC.Infrastructure;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Net;
namespace RAC.Tests;
public sealed class GabrielQuantitativeMemorySqlTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options):IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext()=>new(options);
    }
    private sealed class FreezeClock:TimeProvider
    {
        public override DateTimeOffset GetUtcNow()=>new(2026,11,30,0,0,0,TimeSpan.Zero);
    }
    private sealed class Handler:HttpMessageHandler
    {
        private readonly Dictionary<string,byte[]> archives=new();
        public bool Interrupt {get;set;}
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            Assert.Equal(HttpMethod.Get,request.Method);
            var name=request.RequestUri!.Segments.Last().Replace(".CHECKSUM","");
            var month=int.Parse(name.Substring(name.Length-6,2));
            if(Interrupt && month>=6)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            if(!archives.TryGetValue(name,out var bytes))
            {
                using var stream=new MemoryStream();
                using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))
                using(var writer=new StreamWriter(zip.CreateEntry(name.Replace(".zip",".csv")).Open()))
                {
                    var first=new DateTimeOffset(2026,month,1,0,0,0,TimeSpan.Zero).ToUnixTimeMilliseconds()*1000;
                    for(var i=0;i<DateTime.DaysInMonth(2026,month)*1440;i++)
                    {
                        var t=first+i*60000000L;
                        writer.WriteLine($"{t},100,100,100,100,10,{t+59999999},1000,1,0,0,0");
                    }
                }
                archives.Add(name,bytes=stream.ToArray());
            }
            HttpContent content=request.RequestUri.AbsolutePath.EndsWith(".CHECKSUM")?
                new StringContent(Convert.ToHexString(SHA256.HashData(bytes))+"  "+name):new ByteArrayContent(bytes);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=content});
        }
    }
    [Fact]
    public async Task PersistsFullMatrixAndRecoversPartialResearchWithoutDuplicates()
    {
        if(!OperatingSystem.IsWindows())return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=RAC_GABRIEL_MEMORY_TEST_"+Guid.NewGuid().ToString("N")+";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options))await db.Database.MigrateAsync();
            using var handler=new Handler{Interrupt=true};
            using var http=new HttpClient(handler);
            var source=new GabrielDynamicHistoricalSource(new GabrielPublicArchiveClient(http),new ResearchQuantitativeExecutionStore(new Factory(options)),new ResearchExecutionManifestStore(new Factory(options)),new IndependentResearchFreezeStore(new Factory(options),new FreezeClock()));
            await Assert.ThrowsAsync<HttpRequestException>(()=>source.ReadAsync(["ABCUSDT"],default));
            await using(var db=new RacDbContext(options))Assert.Equal(24,await db.ResearchQuantitativeExecutions.CountAsync());
            await using(var db=new RacDbContext(options))Assert.False(await db.ResearchExecutionManifests.AnyAsync());
            handler.Interrupt=false;
            var first=await source.ReadAsync(["ABCUSDT"],default);
            var second=await source.ReadAsync(["ABCUSDT"],default);
            Assert.Equal(first,second);
            await using var check=new RacDbContext(options);
            Assert.Equal(144,await check.ResearchQuantitativeExecutions.CountAsync());
            Assert.Equal(144,(await check.ResearchExecutionManifests.SingleAsync()).Executions);
            Assert.Empty(RAC.Application.GabrielStrategyRanking.FreezeCandidates(RAC.Application.GabrielExperimentMatrix.Read(first,["ABCUSDT"])));
            Assert.False(await check.IndependentResearchFreezes.AnyAsync());
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally {await using var db=new RacDbContext(options);await db.Database.EnsureDeletedAsync();}
    }

    [Fact]
    public async Task PersistsTwentyAssetMatrixAtScale()
    {
        if(!OperatingSystem.IsWindows())return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=RAC_GABRIEL_SCALE_TEST_"+Guid.NewGuid().ToString("N")+";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options))await db.Database.MigrateAsync();
            using var handler=new Handler();
            using var http=new HttpClient(handler);
            var source=new GabrielDynamicHistoricalSource(new GabrielPublicArchiveClient(http),new ResearchQuantitativeExecutionStore(new Factory(options)),new ResearchExecutionManifestStore(new Factory(options)));
            var symbols=Enumerable.Range(0,20).Select(i=>$"SYN{i:D2}USDT").ToArray();
            var clock=System.Diagnostics.Stopwatch.StartNew();
            await source.ReadAsync(symbols,default);
            clock.Stop();
            await using var check=new RacDbContext(options);
            Assert.Equal(2880,await check.ResearchQuantitativeExecutions.CountAsync());
            var manifest=await check.ResearchExecutionManifests.SingleAsync();
            Assert.Equal(2880,manifest.Executions);
            using var document=System.Text.Json.JsonDocument.Parse(manifest.EvidenceJson);
            Assert.Equal("RAC_MANIFEST_RESULTS_V2",document.RootElement.GetProperty("Schema").GetString());
            var hashes=document.RootElement.GetProperty("Results").EnumerateObject().ToDictionary(x=>x.Name,x=>x.Value.GetString(),StringComparer.Ordinal);
            Assert.Equal(2880,hashes.Count);
            foreach(var record in await check.ResearchQuantitativeExecutions.AsNoTracking().ToListAsync())
            {
                var result=System.Text.Json.JsonSerializer.Deserialize<RAC.Application.ResearchExperimentReplay.Result>(record.ResultJson)!;
                Assert.Equal(RAC.Application.ResearchResultDigest.Create(record.ExecutionId,result),hashes[record.ExecutionId]);
            }
            Assert.Equal(2880,hashes.Values.Distinct().Count());
            Assert.Equal(480,await check.ResearchQuantitativeExecutions.Select(x=>x.ConfigurationId).Distinct().CountAsync());
            Assert.Equal(20,await check.ResearchQuantitativeExecutions.Select(x=>x.Symbol).Distinct().CountAsync());
            Assert.False(await check.TestnetOrders.AnyAsync());
            Console.WriteLine($"Quantitative scale: 2880 records in {clock.Elapsed.TotalSeconds:F2}s");
        }
        finally {await using var db=new RacDbContext(options);await db.Database.EnsureDeletedAsync();}
    }
}
