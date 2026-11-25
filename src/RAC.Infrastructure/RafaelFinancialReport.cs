using RAC.Application;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace RAC.Infrastructure;

public static class RafaelFinancialReport
{
    public static RacAgentMessage Create(Guid taskId,DateTimeOffset observedUtc,RafaelStoredCapital? data,
        RafaelAllocationLimits limits,TimeSpan maximumAge,decimal tolerance)
    {
        string content;
        if(data is null)
            content=JsonSerializer.Serialize(new {Mode="RESEARCH_ONLY",TradingAuthorized=false,Status="NO_PAPER_ACCOUNT"});
        else
        {
            var capital=RafaelCapitalAnalysis.Evaluate(data.Snapshot);
            var health=RafaelFinancialHealth.Evaluate(data.Snapshot,data.StoredEquity,data.UpdatedUtc,
                data.LastCycleUtc,data.Running,observedUtc,maximumAge,tolerance);
            var allocation=RafaelAllocationCapacity.Evaluate(data.Snapshot,limits);
            content=JsonSerializer.Serialize(new {Mode="RESEARCH_ONLY",TradingAuthorized=false,
                Currency="BRL",Valuation="LAST_STORED_PRICE",data.UpdatedUtc,data.LastCycleUtc,
                data.Snapshot.Cash,data.Snapshot.Contributions,data.Snapshot.MarkedPositions,
                capital.Equity,capital.NetResult,capital.ExposurePercent,data.RealizedNetProfit,
                data.PositionCount,data.FillCount,data.Snapshot.RiskLocked,health.DataConsistent,
                health.Findings,ResearchCapacity=health.ResearchAllocationEligible?allocation.ResearchCapacity:0m,
                Limits=limits});
        }
        var message=RacAgentCommunication.Create("rafael","carlos",RacMessageKind.Report,content,taskId,observedUtc);
        return message with {Id=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("RAFAEL:REPORT:V1:"+taskId.ToString("N"))).AsSpan(0,16))};
    }
}
