using RAC.Application;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace RAC.Infrastructure;
public static class MiguelAuditReport
{
    public static RacAgentMessage Create(Guid taskId,MiguelStoredAudit? data,
        DateTimeOffset observedUtc,TimeSpan maximumAge)
    {
        var health=MiguelAuditHealth.Evaluate(data,observedUtc,maximumAge);
        var findings=health.Findings.GroupBy(f=>new {Code=f.Code.Split(':')[0],f.Severity})
            .OrderBy(g=>g.Key.Code,StringComparer.Ordinal).ThenBy(g=>g.Key.Severity,StringComparer.Ordinal)
            .Select(g=>new {g.Key.Code,g.Key.Severity,Count=g.Count()}).ToArray();
        var content=JsonSerializer.Serialize(new {
            Mode="RESEARCH_ONLY",TradingAuthorized=false,Currency="BRL",Valuation="LAST_STORED_PRICE",
            health.Status,health.AccountingConsistent,health.AccountTimestampsFresh,health.HumanReviewRequired,
            RiskLocked=data?.RiskLocked,Running=data?.Running,UpdatedUtc=data?.UpdatedUtc,LastCycleUtc=data?.LastCycleUtc,
            FillCount=data?.FillCount,PositionCount=data?.PositionCount,
            CalculatedEquity=data?.Audit.Equity.CalculatedEquity,RecordedEquity=data?.Audit.Equity.RecordedEquity,
            EquityDifference=data?.Audit.Equity.Difference,NetResult=data?.Audit.Equity.NetResult,
            ExpectedCash=data?.Audit.Movements.ExpectedCash,CashDifference=data?.Audit.Movements.CashDifference,
            Realized=data?.Audit.Movements.CalculatedRealized,Unrealized=data?.Audit.Positions.Unrealized,
            ProfitDifference=data?.Audit.ProfitDifference,TotalFees=data?.Audit.Movements.TotalFees,
            FindingCount=health.Findings.Count,Findings=findings.Take(20),FindingsTruncated=findings.Length>20});
        var message=RacAgentCommunication.Create("miguel","carlos",RacMessageKind.Report,content,taskId,observedUtc);
        return message with {Id=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("MIGUEL:REPORT:V1:"+taskId.ToString("N"))).AsSpan(0,16))};
    }
}
