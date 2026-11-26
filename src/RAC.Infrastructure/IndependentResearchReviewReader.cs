using Microsoft.EntityFrameworkCore;
using RAC.Application;
using System.Text.Json;
namespace RAC.Infrastructure;

// Read-only review of a single audited pair. Never grants trading permission.
public sealed class IndependentResearchReviewReader(IDbContextFactory<RacDbContext> factory,IndependentResearchFreezeStore freezes,TimeProvider clock)
{
    public sealed record Review(IndependentResearchClassification.Assessment Classification,IndependentResearchDeterioration.Assessment Deterioration,IndependentResearchRecommendations.Report Recommendations)
    {
        public bool TradingAuthorized=>false;
    }
    public async Task<Review> ReadAsync(ResearchExperimentPlan.Row stress,string manifestId,CancellationToken ct)
    {
        var ids=await freezes.AuditPairAsync(stress,manifestId,ct);
        var now=clock.GetUtcNow();
        await using var db=await factory.CreateDbContextAsync(ct);
        var keys=ids.ToArray();
        var configurations=await db.IndependentResearchFreezes.AsNoTracking().Where(x=>keys.Contains(x.FreezeId)).ToListAsync(ct);
        var records=await db.IndependentResearchEvidence.AsNoTracking().Where(x=>keys.Contains(x.FreezeId)).Take(13).ToListAsync(ct);
        if(configurations.Count!=2 || records.Count>12)throw new InvalidOperationException("Invalid independent review cohort.");
        var evidence=new List<IndependentResearchGate.Evidence>();
        foreach(var item in records)
        {
            var frozen=configurations.Single(x=>x.FreezeId==item.FreezeId);
            var parameters=JsonSerializer.Deserialize<ResearchBacktestParameters.Experiment>(frozen.ParametersJson)??throw new InvalidOperationException("Missing parameters.");
            var row=new ResearchExperimentPlan.Row(frozen.Symbol,parameters);
            var metrics=JsonSerializer.Deserialize<IndependentResearchGate.Evidence>(item.EvidenceJson)??throw new InvalidOperationException("Missing evidence.");
            var record=new IndependentResearchArchiveEvidence.Record(item.ExecutionId,item.FreezeId,item.SourceSha256,metrics,item.Digest);
            if(item.Month!=metrics.Month || item.RecordedUtc>now || item.Month.AddMonths(1)>DateOnly.FromDateTime(item.RecordedUtc.UtcDateTime) ||
                !IndependentResearchArchiveEvidence.Verify(record,row,frozen.ManifestId,frozen.FrozenUtc))
                throw new InvalidOperationException("Independent evidence audit failed.");
            evidence.Add(metrics);
        }
        return new(IndependentResearchClassification.Evaluate(evidence,now),IndependentResearchDeterioration.Evaluate(evidence),IndependentResearchRecommendations.Evaluate(evidence,now));
    }
}
