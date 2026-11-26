using System.Security.Cryptography;
using System.Text;
namespace RAC.Application;

// Integrity binding only; archive origin and freeze registration require independent audit.
public static class IndependentResearchArchiveEvidence
{
    public sealed record Record(string ExecutionId,string FreezeId,string SourceSha256,
        IndependentResearchGate.Evidence Evidence,string Digest)
    {
        public bool TradingAuthorized=>false;
    }
    public static Record Replay(ResearchExperimentPlan.Row row,DateOnly month,byte[] archive,string sha256,
        string historicalManifestId,DateTimeOffset frozenAt,DateTimeOffset asOf,CancellationToken ct=default)
    {
        if(month.Day!=1 || month<new DateOnly(2026,12,1) || month>=new DateOnly(2027,6,1) ||
            month.AddMonths(1)>DateOnly.FromDateTime(asOf.UtcDateTime))
            throw new ArgumentException("Independent month is not complete.");
        var freeze=IndependentResearchFreeze.Create(row,ResearchExperimentReplay.HistoricalEngineVersion,historicalManifestId,frozenAt);
        var execution=ResearchExperimentExecutionIdentity.Create(row,sha256,month,ResearchExperimentReplay.HistoricalEngineVersion);
        ct.ThrowIfCancellationRequested();
        var data=GabrielHistoricalArchiveReader.Read(row.Symbol,month.Year,month.Month,archive,sha256);
        var evidence=IndependentResearchGate.ReplayMonth(month,row,data,ct);
        return new(execution,freeze,sha256.ToUpperInvariant(),evidence,Digest(execution,freeze,evidence));
    }
    public static string Digest(string execution,string freeze,IndependentResearchGate.Evidence evidence)
    {
        if(new[]{execution,freeze}.Any(x=>x is null||x.Length!=64||!x.All(Uri.IsHexDigit)))
            throw new ArgumentException("Invalid evidence identity.");
        var canonical=FormattableString.Invariant($"RAC:INDEPENDENT:V1|{execution.ToUpperInvariant()}|{freeze.ToUpperInvariant()}|{evidence.Month:yyyy-MM-dd}|{evidence.Cost:G29}|{evidence.Trades}|{evidence.Profit:G29}|{evidence.Loss:G29}|{evidence.DrawdownPct:G29}|{(evidence.Unresolved?1:0)}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
