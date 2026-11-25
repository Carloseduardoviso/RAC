namespace RAC.Application;

public sealed record GabrielResearchAssessment(int Observations,decimal? MeanReturnPercent,decimal? WinRatePercent,string Status)
{
    public bool TradingAuthorized => false;
}

public static class GabrielQuantitativeResearch
{
    public static GabrielResearchAssessment Evaluate(IReadOnlyList<decimal> netReturnsPercent)
    {
        if(netReturnsPercent.Any(x=>x < -100m || x > 10000m))
            throw new ArgumentException("Invalid research return.");
        if(netReturnsPercent.Count==0) return new(0,null,null,"NO_EVIDENCE");
        var mean=netReturnsPercent.Average();
        var wins=100m*netReturnsPercent.Count(x=>x>0)/netReturnsPercent.Count;
        return new(netReturnsPercent.Count,mean,wins,
            netReturnsPercent.Count<30?"INSUFFICIENT_SAMPLE":
            mean<=0?"NON_POSITIVE_NET_RETURN":"INDEPENDENT_VALIDATION_REQUIRED");
    }
}
