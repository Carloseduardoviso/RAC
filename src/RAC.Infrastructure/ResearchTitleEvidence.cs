using System.Text.RegularExpressions;
namespace RAC.Infrastructure;
public static class ResearchTitleEvidence
{
    public sealed record Hint(string Category,string Term,string Evidence);
    private static readonly (string Category,string Term,string Pattern)[] Rules=[
        ("METHOD","Machine learning",@"\bmachine learning\b"),
        ("METHOD","Deep learning",@"\bdeep learning\b"),
        ("METHOD","Reinforcement learning",@"\breinforcement learning\b"),
        ("VALIDATION","Out of sample",@"\bout[- ]of[- ]sample\b"),
        ("VALIDATION","Backtesting",@"\bbacktest(?:ing|s)?\b"),
        ("RISK","Drawdown",@"\bdrawdown\b"),
        ("RISK","Volatility",@"\bvolatility\b"),
        ("INDICATOR","RSI",@"\b(?:RSI|relative strength index)\b"),
        ("INDICATOR","MACD",@"\bMACD\b"),
        ("COST","Transaction costs",@"\btransaction costs?\b"),
        ("COST","Slippage",@"\bslippage\b")];
    public static IReadOnlyList<Hint> Extract(string? title)
    {
        if(string.IsNullOrWhiteSpace(title)||title.Length>1000)return [];
        return Rules.Select(r=>(Rule:r,Match:Regex.Match(title,r.Pattern,RegexOptions.IgnoreCase|RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))))
            .Where(x=>x.Match.Success).Select(x=>new Hint(x.Rule.Category,x.Rule.Term,x.Match.Value)).ToArray();
    }
}
