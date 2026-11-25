using RAC.Application;

namespace RAC.Infrastructure;

public sealed record GabrielDiscoveryAssessment(string Symbol,string Status,int StressConfigurations,int FurtherResearch)
{
    public bool TradingAuthorized => false;
}
public static class GabrielDiscoveryResearch
{
    public static IReadOnlyList<GabrielDiscoveryAssessment> Assess(RebecaDanielReportResult discovery,IReadOnlyList<GabrielExperimentMatrix.Assessment> matrix)
    {
        if(discovery.Status!="RESEARCH_ONLY" || discovery.CoverageStatus!="RESEARCH_CANDIDATES_AVAILABLE")
            throw new InvalidOperationException("Complete discovery evidence required.");
        var ranked=GabrielStrategyRanking.Rank(matrix).ToLookup(x=>x.Assessment.Symbol,StringComparer.Ordinal);
        return Array.AsReadOnly(discovery.Candidates.Select(candidate=>
        {
            var rows=ranked[candidate.Symbol].ToArray();
            return new GabrielDiscoveryAssessment(candidate.Symbol,rows.Length==0?"MATRIX_EVIDENCE_MISSING":rows.Any(x=>x.Status=="INDEPENDENT_VALIDATION_REQUIRED")?"INDEPENDENT_VALIDATION_REQUIRED":"NO_QUALIFYING_CONFIGURATION",rows.Length,rows.Count(x=>x.Status=="INDEPENDENT_VALIDATION_REQUIRED"));
        }).ToArray());
    }
}
