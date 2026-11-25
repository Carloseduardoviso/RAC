using RAC.Application;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace RAC.Tests;

public sealed class GabrielCompleteHistoricalMatrixTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public async Task BuildsAllExperimentsFromSixCompleteSyntheticMonths(int count)
    {
        var symbols=new[]{"ABCUSDT","XYZUSDT","DEFUSDT","GHIUSDT","JKLUSDT","MNOUSDT","PQRUSDT","STUUSDT","VWXUSDT","YZABUSDT","AACUSDT","BBCUSDT","CCCUSDT","DDCUSDT","EECUSDT","FFCUSDT","GGCUSDT","HHCUSDT","IICUSDT","JJCUSDT"}.Take(count).ToArray();
        var watch=System.Diagnostics.Stopwatch.StartNew();
        using var process=System.Diagnostics.Process.GetCurrentProcess();
        process.Refresh();
        var initialWorkingSet=process.WorkingSet64;
        var initialCpu=process.TotalProcessorTime;
        var allocated=GC.GetTotalAllocatedBytes(true);
        var months=new List<int>();
        var json=await GabrielHistoricalMatrixBuilder.BuildAsync(symbols,
            (symbol,year,month,ct)=>{
                ct.ThrowIfCancellationRequested();
                Assert.Contains(symbol,symbols);
                Assert.Equal(2026,year);
                months.Add(month);
                using var stream=new MemoryStream();
                using(var zip=new ZipArchive(stream,ZipArchiveMode.Create,true))
                using(var writer=new StreamWriter(zip.CreateEntry($"{symbol}-1m-{year:D4}-{month:D2}.csv").Open()))
                {
                    var first=new DateTimeOffset(year,month,1,0,0,0,TimeSpan.Zero).ToUnixTimeMilliseconds()*1000;
                    for(var i=0;i<DateTime.DaysInMonth(year,month)*1440;i++)
                    {
                        var t=first+i*60000000L;
                        writer.WriteLine($"{t},100,100,100,100,10,{t+59999999},1000,1,0,0,0");
                    }
                }
                var bytes=stream.ToArray();
                return Task.FromResult(new GabrielHistoricalMatrixBuilder.Archive(bytes,Convert.ToHexString(SHA256.HashData(bytes))));
            },default);
        Assert.Equal(Enumerable.Range(5,6).SelectMany(m=>Enumerable.Repeat(m,count)),months);
        using var doc=JsonDocument.Parse(json);
        Assert.False(doc.RootElement.GetProperty("TradingAuthorized").GetBoolean());
        Assert.False(doc.RootElement.GetProperty("IndependentValidation").GetBoolean());
        Assert.Equal(144*count,doc.RootElement.GetProperty("Rows").GetArrayLength());
        var matrix=GabrielExperimentMatrix.Read(json,symbols);
        Assert.Equal(24*count,matrix.Count);
        Assert.All(matrix,row=>Assert.Contains(row.Symbol,symbols));
        process.Refresh();
        Console.WriteLine($"Process metrics: initial working set {initialWorkingSet}; final working set {process.WorkingSet64}; lifetime peak working set {process.PeakWorkingSet64}; CPU seconds {(process.TotalProcessorTime-initialCpu).TotalSeconds:F2}");
        Console.WriteLine($"Historical probe: {count} symbols; {watch.Elapsed.TotalSeconds:F2}s; process-wide allocated bytes delta {GC.GetTotalAllocatedBytes(true)-allocated}");
    }

    [Fact]
    public void ReplayRejectsCancelledWork()
    {
        using var cts=new CancellationTokenSource();
        cts.Cancel();
        var plan=ResearchExperimentPlan.Build(["ABCUSDT"]);
        Assert.ThrowsAny<OperationCanceledException>(()=>ResearchExperimentReplay.Run([],plan.First().Parameters,ct:cts.Token));
    }

    [Fact]
    public void ReplayCancelsDuringActiveProcessing()
    {
        using var cts=new CancellationTokenSource();
        var data=new CancellingMinutes(cts);
        var plan=ResearchExperimentPlan.Build(["ABCUSDT"]);
        Assert.ThrowsAny<OperationCanceledException>(()=>ResearchExperimentReplay.Run(data,plan.First().Parameters,ct:cts.Token));
        Assert.InRange(data.IndexReads,1,10000);
    }

    private sealed class CancellingMinutes(CancellationTokenSource cts):IReadOnlyList<ResearchExperimentReplay.Minute>
    {
        private static readonly ResearchExperimentReplay.Minute Candle=new(100,100,1000);
        public int IndexReads {get;private set;}
        public int Count=>44640;
        public ResearchExperimentReplay.Minute this[int index]
        {
            get
            {
                if(++IndexReads==100) cts.Cancel();
                return Candle;
            }
        }
        public IEnumerator<ResearchExperimentReplay.Minute> GetEnumerator()=>Enumerable.Repeat(Candle,Count).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()=>GetEnumerator();
    }
}
