using RAC.Application;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
namespace RAC.Infrastructure;
public static class LucasConnectivityReport
{
    public static RacAgentMessage Create(Guid taskId,LucasConnectivitySnapshot snapshot)
    {
        var endpoints=snapshot.Endpoints.Select(x=>new {
            x.Exchange,x.Environment,x.Health.Status,
            LatencyMilliseconds=x.Health.Findings.Contains("MISSING_OBSERVATION")?(decimal?)null:x.Health.LatencyMilliseconds,
            x.Health.ClockOffsetMilliseconds,x.Health.Findings}).ToArray();
        var content=JsonSerializer.Serialize(new {
            Mode="RESEARCH_ONLY",TradingAuthorized=false,Scope="ANONYMOUS_PUBLIC_TIME",
            snapshot.Status,snapshot.HumanReviewRequired,snapshot.ObservedUtc,
            EndpointCount=endpoints.Length,Endpoints=endpoints});
        if(content.Length>4096) throw new ArgumentException("Connectivity report exceeds message capacity.");
        var message=RacAgentCommunication.Create("lucas","ana",RacMessageKind.Report,content,taskId,snapshot.ObservedUtc);
        return message with {Id=new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("LUCAS:REPORT:V1:"+taskId.ToString("N"))).AsSpan(0,16))};
    }
}
