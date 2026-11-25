using RAC.Application;
using RAC.Infrastructure;
using System.Text.Json;
namespace RAC.Tests;
public sealed class SamuelLiquidityReportTests
{
    [Fact]
    public void ReportsCostsAndIncompleteDepthToCarlos()
    {
        var now=DateTimeOffset.UtcNow;
        var id=Guid.NewGuid();
        var book=new SamuelDepthSnapshot(1,now,[new(99,1)],[new(100,1)]);
        var report=SamuelLiquidityReport.Create(id,"BTCUSDT",book,[1,2],1,2,2,now);
        Assert.Equal("samuel",report.Sender);
        Assert.Equal("carlos",report.Recipient);
        Assert.Equal(id,report.CorrelationId);
        Assert.Equal(report,SamuelLiquidityReport.Create(id,"BTCUSDT",book,[2,1],1,2,2,now));
        using var doc=JsonDocument.Parse(report.Content);
        Assert.False(doc.RootElement.GetProperty("TradingAuthorized").GetBoolean());
        var rows=doc.RootElement.GetProperty("Scenarios");
        Assert.Equal("EXCESSIVE_COST",rows[0].GetProperty("Status").GetString());
        Assert.Equal("INSUFFICIENT_DEPTH",rows[1].GetProperty("Status").GetString());
        Assert.Equal(JsonValueKind.Null,rows[1].GetProperty("NetResult").ValueKind);
        Assert.Throws<ArgumentException>(()=>SamuelLiquidityReport.Create(id,"BTCUSDT",book,[1,2,3,4,5,6],1,2,2,now));
    }
}
