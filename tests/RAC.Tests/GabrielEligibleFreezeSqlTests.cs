using Microsoft.EntityFrameworkCore;
using RAC.Infrastructure;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Net;
namespace RAC.Tests;
public sealed class GabrielEligibleFreezeSqlTests
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
                        var t=first+i*60000000L; var price=(i/60)%24<12?100+(i/60)%24:112-((i/60)%24-12);
                        writer.WriteLine($"{t},{price},{price},{price},{price},10000,{t+59999999},1000000,1,0,0,0");
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
    public async Task EligibleHistoricalCandidatesAreFrozenExactlyOnce()
    {
        if(!OperatingSystem.IsWindows())return;
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=RAC_ELIGIBLE_FREEZE_TEST_"+Guid.NewGuid().ToString("N")+";Integrated Security=True;TrustServerCertificate=True").Options;
        try
        {
            await using(var db=new RacDbContext(options))await db.Database.MigrateAsync();
            using var handler=new Handler();
            using var http=new HttpClient(handler);
            var factory=new Factory(options);
            var source=new GabrielDynamicHistoricalSource(new GabrielPublicArchiveClient(http),new ResearchQuantitativeExecutionStore(factory),new ResearchExecutionManifestStore(factory),new IndependentResearchFreezeStore(factory,new FreezeClock()));
            var first=await source.ReadAsync(["ABCUSDT"],default);
            var candidates=RAC.Application.GabrielStrategyRanking.FreezeCandidates(RAC.Application.GabrielExperimentMatrix.Read(first,["ABCUSDT"]));
            Assert.NotEmpty(candidates);
            Assert.Equal(first,await source.ReadAsync(["ABCUSDT"],default));
            await using var check=new RacDbContext(options);
            var frozen=await check.IndependentResearchFreezes.ToListAsync();
            Assert.Equal(candidates.Count,frozen.Count);
            var manifest=await check.ResearchExecutionManifests.SingleAsync();
            foreach(var candidate in candidates)
            {
                var id=RAC.Application.ResearchExperimentIdentity.Create(candidate);
                Assert.Contains(frozen,x=>x.ConfigurationId==id && x.ManifestId==manifest.ManifestId);
            }
            Assert.False(await check.TestnetOrders.AnyAsync());
        }
        finally {await using var db=new RacDbContext(options);await db.Database.EnsureDeletedAsync();}
    }
}
