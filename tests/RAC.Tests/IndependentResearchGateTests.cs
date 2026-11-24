using RAC.Application;
public class IndependentResearchGateTests
{
    private static IndependentResearchGate.Evidence[] Sample()=>Enumerable.Range(0,6).SelectMany(i=>new[]{.0015m,.003m}.Select(c=>new IndependentResearchGate.Evidence(new DateOnly(2026,12,1).AddMonths(i),c,5,3,1,1,false))).ToArray();
    [Fact] public void CompleteEvidencePassesResearchCriteria()=>Assert.Empty(IndependentResearchGate.Evaluate(Sample()));
    [Fact] public void MissingEvidenceFails()=>Assert.NotEmpty(IndependentResearchGate.Evaluate(Sample()[1..]));
    [Fact] public void DuplicateEvidenceFails(){var r=Sample();r[0]=r[1];Assert.NotEmpty(IndependentResearchGate.Evaluate(r));}
    [Fact] public void OpenPositionFails(){var r=Sample();r[0]=r[0] with {Unresolved=true};Assert.Contains("RISK_OR_UNRESOLVED_POSITION",IndependentResearchGate.Evaluate(r));}
    [Fact] public void LowTradeCountFails()=>Assert.Contains("INSUFFICIENT_TRADES",IndependentResearchGate.Evaluate(Sample().Select(r=>r with {Trades=1}).ToArray()));
    [Fact] public void LowProfitFails()=>Assert.Contains("INSUFFICIENT_PROFIT_FACTOR",IndependentResearchGate.Evaluate(Sample().Select(r=>r with {Profit=1}).ToArray()));
}
