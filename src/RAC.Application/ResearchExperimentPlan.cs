using RAC.Infrastructure;
using RAC.Domain;
namespace RAC.Application;
public static class ResearchExperimentPlan
{
    public sealed record Row(string Symbol,ResearchBacktestParameters.Experiment Parameters);
    public static IReadOnlyList<Row> Build()
    {
        var templates=new[]{"MA_CROSS_V1","MEAN_REVERSION_V1","MOMENTUM_V1"};
        return (from symbol in RacMarketSymbols.Supported
                from template in templates
                from experiment in ResearchBacktestParameters.Generate(new("","",template,"","","HYPOTHESIS_UNVALIDATED",false))
                select new Row(symbol,experiment)).ToArray();
    }
}
