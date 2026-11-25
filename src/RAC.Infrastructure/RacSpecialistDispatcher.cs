using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;

namespace RAC.Infrastructure;

public sealed class RacSpecialistDispatcher(IDbContextFactory<RacDbContext> factory, string? onlySpecialist = null, string? secondSpecialist = null, string? thirdSpecialist = null, string? fourthSpecialist = null, string? fifthSpecialist = null, string? sixthSpecialist = null)
{
    public async Task<int> DispatchAsync(DateTimeOffset now,CancellationToken ct)
    {
        int count=0;
        foreach(var schedule in RacSpecialistSchedulePlan.Entries)
        {
            if(onlySpecialist is not null && schedule.Specialist!=onlySpecialist && schedule.Specialist!=secondSpecialist && schedule.Specialist!=thirdSpecialist && schedule.Specialist!=fourthSpecialist && schedule.Specialist!=fifthSpecialist && schedule.Specialist!=sixthSpecialist) continue;
            var task=RacSpecialistSchedulePlan.ForWindow(schedule,now);
            await using var db=await factory.CreateDbContextAsync(ct);
            await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
            if(await db.AgentTasks.AnyAsync(x=>x.Id==task.Id ||
                (x.Specialist==task.Specialist && (x.Status=="Pending" || x.Status=="Running")),ct))
                continue;
            db.AgentTasks.Add(new RacAgentTaskRecord
            {
                Id=task.Id,Leader=task.Leader,Specialist=task.Specialist,
                Description=task.Description,Status=task.Status.ToString(),
                Attempts=0,UpdatedUtc=task.UpdatedUtc
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            count++;
        }
        return count;
    }
}
