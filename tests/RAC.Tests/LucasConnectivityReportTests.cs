using RAC.Application;
using RAC.Infrastructure;
using System.Text.Json;
namespace RAC.Tests;
public sealed class LucasConnectivityReportTests
{
    [Fact]
    public void ReportsMissingEndpointToAnaWithoutTradingPermission()
    {
        var now=DateTimeOffset.UtcNow;
        var id=Guid.NewGuid();
        var snapshot=LucasConnectivityAnalysis.Evaluate(new[]{("BINANCE","PUBLIC"),("BINANCE","TESTNET")},[],now,TimeSpan.FromSeconds(30),TimeSpan.FromSeconds(1),TimeSpan.FromMilliseconds(500));
        var report=LucasConnectivityReport.Create(id,snapshot);
        Assert.Equal("lucas",report.Sender);
        Assert.Equal("ana",report.Recipient);
        Assert.Equal(id,report.CorrelationId);
        Assert.Equal(report,LucasConnectivityReport.Create(id,snapshot));
        using var json=JsonDocument.Parse(report.Content);
        var root=json.RootElement;
        Assert.False(root.GetProperty("TradingAuthorized").GetBoolean());
        Assert.True(root.GetProperty("HumanReviewRequired").GetBoolean());
        Assert.Equal("ANONYMOUS_PUBLIC_TIME",root.GetProperty("Scope").GetString());
        Assert.Equal(2,root.GetProperty("EndpointCount").GetInt32());
        Assert.Equal(JsonValueKind.Null,root.GetProperty("Endpoints")[0].GetProperty("LatencyMilliseconds").ValueKind);
    }
}
