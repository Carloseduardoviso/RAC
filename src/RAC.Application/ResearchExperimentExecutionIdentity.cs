using System.Security.Cryptography;
using System.Text;
namespace RAC.Application;
public static class ResearchExperimentExecutionIdentity
{
    public static string Create(ResearchExperimentPlan.Row row,string sourceSha256,DateOnly month,string engineVersion)
    {
        if(sourceSha256 is null || sourceSha256.Length!=64 || !sourceSha256.All(Uri.IsHexDigit))
            throw new ArgumentException("Invalid source digest.",nameof(sourceSha256));
        if(month.Day!=1) throw new ArgumentException("Expected first day of month.",nameof(month));
        if(string.IsNullOrEmpty(engineVersion)||engineVersion.Length>64||engineVersion.Any(c=>!(c>='A'&&c<='Z')&&!(c>='0'&&c<='9')&&c!='_'&&c!='.'))
            throw new ArgumentException("Invalid engine version.",nameof(engineVersion));
        var configuration=ResearchExperimentIdentity.Create(row);
        var canonical=FormattableString.Invariant($"RAC:EXECUTION:V1|{configuration}|{sourceSha256.ToUpperInvariant()}|{month:yyyy-MM-dd}|{engineVersion}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
