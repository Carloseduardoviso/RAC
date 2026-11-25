using RAC.Application;
using RAC.Infrastructure;
using System.Text.Json;
namespace RAC.Tests;
public sealed class RafaelFinancialReportTests
{
    [Fact]
    public void ReportsToCarlosAndSuppressesCapacityWhenStaleOrLocked()
    {
        var now=DateTimeOffset.UtcNow;
        var id=Guid.NewGuid();
        var data=new RafaelStoredCapital(new(80,20,100,false),now,now,true,1,2,0,100);
        var limits=new RafaelAllocationLimits(30,40,15);
        RacAgentMessage Report(RafaelStoredCapital? value)=>RafaelFinancialReport.Create(id,now,value,limits,TimeSpan.FromMinutes(2),0.01m);
        var report=Report(data);
        Assert.Equal("rafael",report.Sender);
        Assert.Equal("carlos",report.Recipient);
        Assert.Equal(report,Report(data));
        using var json=JsonDocument.Parse(report.Content);
        Assert.Equal(15m,json.RootElement.GetProperty("ResearchCapacity").GetDecimal());
        Assert.False(json.RootElement.GetProperty("TradingAuthorized").GetBoolean());
        using var stale=JsonDocument.Parse(Report(data with {UpdatedUtc=now.AddMinutes(-3)}).Content);
        Assert.Equal(0m,stale.RootElement.GetProperty("ResearchCapacity").GetDecimal());
        using var locked=JsonDocument.Parse(Report(data with {Snapshot=data.Snapshot with {RiskLocked=true}}).Content);
        Assert.Equal(0m,locked.RootElement.GetProperty("ResearchCapacity").GetDecimal());
        Assert.Contains("NO_PAPER_ACCOUNT",Report(null).Content);
    }
}
