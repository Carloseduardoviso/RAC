namespace RAC.Application;

// Deterministic task plan; does not execute or authorize trading.
public static class RacCarlosReviewTasks
{
    public static IReadOnlyList<RacAgentTask> Create(Guid requestId, DateTimeOffset now)
    {
        if(requestId==Guid.Empty) throw new ArgumentException("Invalid review request ID.");
        return Array.AsReadOnly(RacSpecialistSchedulePlan.Entries.Where(x=>x.Leader=="carlos").Select(x=>
        {
            var bytes=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("RAC:CARLOS:REVIEW:V1:"+requestId.ToString("N")+":"+x.Specialist));
            return RacTaskLifecycle.Create(x.Leader,x.Specialist,x.Activity,now) with {Id=new Guid(bytes.AsSpan(0,16))};
        }).ToArray());
    }
}
