namespace RAC.Infrastructure;
public static class ResearchStrategyCandidates
{
    public sealed record Candidate(string Doi,string Concept,string TemplateId,string EntryRule,string ExitRule,string Status,bool TradingAuthorized);
    public static IReadOnlyList<Candidate> Classify(string doi,IReadOnlyList<ResearchContextAnalysis.Observation> observations)
    {
        if(string.IsNullOrWhiteSpace(doi)||doi.Length>255||!doi.StartsWith("10.",StringComparison.Ordinal)||!doi.Contains('/')||doi.Any(char.IsWhiteSpace))return [];
        var result=new List<Candidate>();
        foreach(var o in observations.Where(x=>x.Category=="STRATEGY"&&x.Level=="ABSTRACT_MENTION"&&!x.TradingAuthorized).DistinctBy(x=>x.Concept))
        {
            var template=o.Concept switch
            {
                "Moving average crossover"=>("MA_CROSS_V1","Fast average crosses above slow average on closed candle","Reverse crossover or risk exit"),
                "Mean reversion"=>("MEAN_REVERSION_V1","Price below predefined deviation threshold","Return to mean or risk exit"),
                "Momentum"=>("MOMENTUM_V1","Positive return exceeds predefined threshold","Momentum reversal or risk exit"),
                _=>("","","")
            };
            if(template.Item1.Length==0)continue;
            result.Add(new(doi.ToLowerInvariant(),o.Concept,template.Item1,template.Item2,template.Item3,"HYPOTHESIS_UNVALIDATED",false));
        }
        return result;
    }
}
