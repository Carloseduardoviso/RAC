using RAC.Domain;
namespace RAC.Infrastructure;
public static class ResearchProductivity
{
    public sealed record Metric(string Topic,int Attempts,int Completed,int Failed,long Found,long Saved,double? DiscoveryRate,double? AverageElapsedMs);
    public static IReadOnlyList<Metric> Summarize(IEnumerable<ResearchExecutionRecord> records)
    {
        return records.GroupBy(x=>x.Topic,StringComparer.Ordinal).OrderBy(x=>x.Key,StringComparer.Ordinal)
            .Select(g=>{
                var completed=g.Where(x=>x.Status=="COMPLETED").ToArray();
                var found=completed.Sum(x=>(long)(x.PublicationsFound??0));
                var saved=completed.Sum(x=>(long)(x.PublicationsSaved??0));
                return new Metric(g.Key,g.Count(),completed.Length,g.Count(x=>x.Status=="FAILED"),found,saved,
                    found>0?(double)saved/found:null,completed.Length>0?completed.Average(x=>(double)x.ElapsedMs):null);
            }).ToArray();
    }
}
