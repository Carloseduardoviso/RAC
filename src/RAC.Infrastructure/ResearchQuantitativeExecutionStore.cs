using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using System.Text.Json;
namespace RAC.Infrastructure;

public sealed class ResearchQuantitativeExecutionStore(IDbContextFactory<RacDbContext> factory)
{
    public async Task<bool> SaveAsync(ResearchExperimentPlan.Row row,string sourceSha256,DateOnly month,string engineVersion,ResearchExperimentReplay.Result result,DateTimeOffset now,CancellationToken ct)
    {
        var id=ResearchExperimentExecutionIdentity.Create(row,sourceSha256,month,engineVersion);
        if(month>=new DateOnly(now.UtcDateTime.Year,now.UtcDateTime.Month,1)||result.NetPct< -100||result.DrawdownPct<0||result.DrawdownPct>100||result.Trades<0||result.Wins<0||result.Wins>result.Trades||result.Costs<0||result.ProfitFactor<0)
            throw new ArgumentException("Invalid historical result.");
        var parameters=JsonSerializer.Serialize(row.Parameters);
        var payload=JsonSerializer.Serialize(result);
        for(var attempt=0;;attempt++)
        {
        try {
        await using var db=await factory.CreateDbContextAsync(ct);
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var existing=await db.ResearchQuantitativeExecutions.SingleOrDefaultAsync(x=>x.ExecutionId==id,ct);
        if(existing is not null)
        {
            if(JsonSerializer.Deserialize<ResearchExperimentReplay.Result>(existing.ResultJson)!=result||existing.ConfigurationId!=ResearchExperimentIdentity.Create(row)||existing.SourceSha256!=sourceSha256.ToUpperInvariant()||existing.Symbol!=row.Symbol||existing.Month!=month||existing.EngineVersion!=engineVersion||JsonSerializer.Deserialize<ResearchBacktestParameters.Experiment>(existing.ParametersJson)!=row.Parameters)
                throw new InvalidOperationException("Conflicting research execution.");
            return false;
        }
        db.ResearchQuantitativeExecutions.Add(new ResearchQuantitativeExecutionRecord{ExecutionId=id,ConfigurationId=ResearchExperimentIdentity.Create(row),SourceSha256=sourceSha256.ToUpperInvariant(),Symbol=row.Symbol,Month=month,EngineVersion=engineVersion,ParametersJson=parameters,ResultJson=payload,RecordedUtc=now.ToUniversalTime()});
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
        } catch(Exception ex) when(attempt<3 && IsRetryable(ex)) { await Task.Delay(50*(attempt+1),ct); }
        }
    }

    private static bool IsRetryable(Exception ex)
    {
        for(Exception? current=ex;current is not null;current=current.InnerException)
            if(current is Microsoft.Data.SqlClient.SqlException sql && sql.Number is 1205 or 2601 or 2627) return true;
        return false;
    }
}
