using RAC.Infrastructure;
using RAC.Domain;
namespace RAC.Application;
public static class ResearchExperimentPlan
{
    public sealed record Row(string Symbol,ResearchBacktestParameters.Experiment Parameters);
    public static IReadOnlyList<Row> Build(IEnumerable<string>? symbols=null)
    {
        var universe=(symbols ?? RacMarketSymbols.Supported).ToArray();
        if(universe.Length==0 || universe.Length>20 ||
            universe.Any(x=>string.IsNullOrWhiteSpace(x) || x.Length<5 || x.Length>40 ||
                !x.EndsWith("USDT",StringComparison.Ordinal) || x.Any(c=>!(c>='A' && c<='Z') && !(c>='0' && c<='9'))) ||
            universe.Distinct(StringComparer.Ordinal).Count()!=universe.Length)
            throw new ArgumentException("Invalid research symbol universe.",nameof(symbols));
        var templates=new[]{"MA_CROSS_V1","MEAN_REVERSION_V1","MOMENTUM_V1"};
        return (from symbol in universe
                from template in templates
                from experiment in ResearchBacktestParameters.Generate(new("","",template,"","","HYPOTHESIS_UNVALIDATED",false))
                select new Row(symbol,experiment)).ToArray();
    }
}
