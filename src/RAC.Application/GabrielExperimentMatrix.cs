using RAC.Infrastructure;
using System.Text.Json;
namespace RAC.Application;

public static class GabrielExperimentMatrix
{
    public sealed record Row(string Month,string Phase,string Symbol,ResearchBacktestParameters.Experiment Parameters,ResearchExperimentReplay.Result Result);
    public sealed record Assessment(string Symbol,ResearchBacktestParameters.Experiment Parameters, GabrielHistoricalAssessment Evidence);
    public static IReadOnlyList<Assessment> Read(string json,IEnumerable<string>? symbols=null)
    {
        using var doc=JsonDocument.Parse(json);
        var root=doc.RootElement;
        if(root.GetProperty("Mode").GetString()!="RESEARCH_ONLY" ||
            root.GetProperty("TradingAuthorized").GetBoolean() || root.GetProperty("IndependentValidation").GetBoolean())
            throw new InvalidDataException("Invalid research mode.");
        var rows=root.GetProperty("Rows").Deserialize<Row[]>() ?? throw new InvalidDataException("Missing rows.");
        var plan=ResearchExperimentPlan.Build(symbols);
        var months=new[]{"2026-05","2026-06","2026-07","2026-08","2026-09","2026-10"};
        if(rows.Length!=plan.Count*months.Length ||
            rows.Select(x=>(x.Month,x.Symbol,x.Parameters)).Distinct().Count()!=rows.Length ||
            rows.Any(x=>!months.Contains(x.Month)||!plan.Contains(new ResearchExperimentPlan.Row(x.Symbol,x.Parameters))))
            throw new InvalidDataException("Incomplete or invalid experiment matrix.");
        return rows.GroupBy(x=>(x.Symbol,x.Parameters))
            .Select(g=>new Assessment(g.Key.Symbol,g.Key.Parameters,
                GabrielHistoricalAnalysis.Evaluate(g.Select(x=>new GabrielMonthlyReplay(x.Month,x.Result)).ToArray())))
            .ToArray();
    }
}
