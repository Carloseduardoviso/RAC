using System.Globalization;
using System.Security.Cryptography;
using System.Text;
namespace RAC.Application;

// Stable identity of a research configuration, not an execution authorization.
public static class ResearchExperimentIdentity
{
    public static string Create(ResearchExperimentPlan.Row row)
    {
        if(!ResearchExperimentPlan.Build([row.Symbol]).Contains(row))
            throw new ArgumentException("Configuration is outside the research plan.",nameof(row));
        var p=row.Parameters;
        var canonical=FormattableString.Invariant($"RAC:EXPERIMENT:V1|{row.Symbol}|{p.TemplateId}|{p.CandleMinutes}|{p.FastPeriod}|{p.SlowPeriod}|{p.ThresholdPct:G29}|{p.StopPct:G29}|{p.TargetPct:G29}|{p.FeePctPerSide:G29}|{p.SlippagePctPerSide:G29}|{p.ExposurePct:G29}|{p.Status}|0");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
