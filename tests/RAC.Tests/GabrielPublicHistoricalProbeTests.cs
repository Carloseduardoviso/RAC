using RAC.Application;
using RAC.Infrastructure;
namespace RAC.Tests;

public sealed class GabrielPublicHistoricalProbeTests
{
    [Fact]
    public async Task ExplicitPublicProbeBuildsCompleteBtcMatrix()
    {
        if(Environment.GetEnvironmentVariable("RAC_GABRIEL_LIVE_PROBE")!="1") return;
        using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var http=new HttpClient(new HttpClientHandler {AllowAutoRedirect=false,UseCookies=false});
        var source=new GabrielDynamicHistoricalSource(new GabrielPublicArchiveClient(http));
        var json=await source.ReadAsync(new[]{"BTCUSDT"},deadline.Token);
        Assert.Equal(24,GabrielExperimentMatrix.Read(json,new[]{"BTCUSDT"}).Count);
    }
}
