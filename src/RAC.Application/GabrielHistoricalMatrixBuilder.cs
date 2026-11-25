using System.Text.Json;
namespace RAC.Application;

public static class GabrielHistoricalMatrixBuilder
{
    public sealed record Archive(byte[] Bytes,string Sha256);
    public static async Task<string> BuildAsync(IReadOnlyList<string> symbols,
        Func<string,int,int,CancellationToken,Task<Archive>> readArchive,CancellationToken ct)
    {
        var plan=ResearchExperimentPlan.Build(symbols);
        var rows=new List<GabrielExperimentMatrix.Row>();
        for(var month=5;month<=10;month++)
        {
            foreach(var group in plan.GroupBy(x=>x.Symbol,StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();
                var archive=await readArchive(group.Key,2026,month,ct);
                var data=GabrielHistoricalArchiveReader.Read(group.Key,2026,month,archive.Bytes,archive.Sha256);
                foreach(var row in group)
                {
                    ct.ThrowIfCancellationRequested();
                    rows.Add(new($"2026-{month:D2}",month<8?"EXPLORATORY_TRAINING":"REUSED_HISTORICAL_EVALUATION",
                        row.Symbol,row.Parameters,ResearchExperimentReplay.Run(data,row.Parameters)));
                }
            }
        }
        var json=JsonSerializer.Serialize(new {Mode="RESEARCH_ONLY",IndependentValidation=false,TradingAuthorized=false,Rows=rows});
        GabrielExperimentMatrix.Read(json,symbols);
        return json;
    }
}
