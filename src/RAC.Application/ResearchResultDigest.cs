using System.Security.Cryptography;
using System.Text;
namespace RAC.Application;
public static class ResearchResultDigest
{
    public static string Create(string executionId,ResearchExperimentReplay.Result result)
    {
        if(executionId is null || executionId.Length!=64 || !executionId.All(Uri.IsHexDigit))
            throw new ArgumentException("Invalid execution identity.",nameof(executionId));
        var profit=result.ProfitFactor?.ToString("G29",System.Globalization.CultureInfo.InvariantCulture)??"NULL";
        var canonical=FormattableString.Invariant($"RAC:RESULT:V1|{executionId.ToUpperInvariant()}|{result.NetPct:G29}|{result.DrawdownPct:G29}|{result.Trades}|{result.Wins}|{profit}|{result.Costs:G29}|{(result.Unresolved?1:0)}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
