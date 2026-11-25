using RAC.Application;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace RAC.Infrastructure;
public static class SamuelLiquidityReport
{
    public static RacAgentMessage Create(Guid taskId,string symbol,SamuelDepthSnapshot book,
        IReadOnlyList<decimal> quantities,decimal feePercent,decimal maxImpactPercent,
        decimal maxCostPercent,DateTimeOffset observedUtc)
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(symbol,@"\A[A-Z0-9]{2,16}USDT\z") ||
            quantities.Count>5 || book.UpdateId<=0)
            throw new ArgumentException("Invalid report inputs.");
        var rows=SamuelLiquidityCapacity.Evaluate(book,quantities,feePercent,maxImpactPercent,maxCostPercent,observedUtc);
        var content=JsonSerializer.Serialize(new {Mode="RESEARCH_ONLY",TradingAuthorized=false,
            Symbol=symbol,Currency="USDT",Model="STATIC_BOOK_ROUND_TRIP",book.UpdateId,book.ReceivedUtc,
            FeePercent=feePercent,MaxImpactPercent=maxImpactPercent,MaxCostPercent=maxCostPercent,
            Scenarios=rows.Select(r=>new {r.Quantity,r.Status,
                EntryFilled=r.Estimate.Entry.Filled,ExitFilled=r.Estimate.Exit.Filled,
                EntryImpact=r.Estimate.Entry.ImpactPercent,ExitImpact=r.Estimate.Exit.ImpactPercent,
                r.Estimate.TotalEntry,r.Estimate.NetExit,r.Estimate.NetResult,r.Estimate.NetReturnPercent,
                r.Estimate.BreakEvenExitAverage})});
        var message=RacAgentCommunication.Create("samuel","carlos",RacMessageKind.Report,content,taskId,observedUtc);
        return message with {Id=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("SAMUEL:REPORT:V1:"+taskId.ToString("N"))).AsSpan(0,16))};
    }
}
