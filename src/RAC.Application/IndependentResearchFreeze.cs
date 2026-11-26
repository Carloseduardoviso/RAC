using System.Security.Cryptography;
using System.Text;
namespace RAC.Application;

// Research-only identity; a digest alone does not prove the time of registration.
public static class IndependentResearchFreeze
{
    public static readonly DateTimeOffset ValidationStart=new(2026,12,1,0,0,0,TimeSpan.Zero);
    public static string Create(ResearchExperimentPlan.Row row,string engineVersion,string historicalManifestId,DateTimeOffset frozenAt)
    {
        ResearchEngineCatalog.Resolve(engineVersion);
        if(frozenAt>=ValidationStart || frozenAt<new DateTimeOffset(2026,11,1,0,0,0,TimeSpan.Zero))
            throw new ArgumentException("Freeze must precede independent validation.",nameof(frozenAt));
        if(historicalManifestId is null || historicalManifestId.Length!=64 || !historicalManifestId.All(Uri.IsHexDigit))
            throw new ArgumentException("Invalid historical manifest.",nameof(historicalManifestId));
        var configuration=ResearchExperimentIdentity.Create(row);
        var canonical=FormattableString.Invariant($"RAC:FREEZE:V1|{configuration}|{engineVersion}|{historicalManifestId.ToUpperInvariant()}|{frozenAt.UtcDateTime:O}|2026-12-01|2027-06-01|RESEARCH_ONLY");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
