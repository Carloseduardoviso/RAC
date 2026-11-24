using System.Text.RegularExpressions;
namespace RAC.Infrastructure;
public static class ResearchContextAnalysis
{
    public sealed record Observation(string Category,string Concept,string Evidence,string Level,bool TradingAuthorized);
    private static readonly (string Category,string Concept,string Pattern)[] Rules=[
        ("STRATEGY","Moving average crossover",@"\bmoving average crossovers?\b"),
        ("STRATEGY","Mean reversion",@"\bmean[- ]reversion\b"),
        ("STRATEGY","Momentum",@"\bmomentum (?:trading|strateg(?:y|ies))\b"),
        ("RISK","Stop loss",@"\bstop[- ]loss\b"),
        ("VALIDATION","Walk forward",@"\bwalk[- ]forward\b"),
        ("COST","Transaction costs",@"\btransaction costs?\b"),
        ("LIMITATION","Overfitting",@"\boverfitting\b"),
        ("METHOD","Machine learning",@"\bmachine learning\b")];
    public static IReadOnlyList<Observation> Analyze(string? text,ResearchAbstractUsagePolicy.Decision permission)
    {
        if(!permission.PersistentStorageAllowed||string.IsNullOrWhiteSpace(text)||text.Length>10000)return [];
        var result=new List<Observation>();
        foreach(var rule in Rules)
        {
            var match=Regex.Match(text,rule.Pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
            if(match.Success)result.Add(new(rule.Category,rule.Concept,match.Value,"ABSTRACT_MENTION",false));
        }
        return result;
    }
}
