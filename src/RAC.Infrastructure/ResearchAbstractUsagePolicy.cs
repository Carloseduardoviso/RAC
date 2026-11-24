namespace RAC.Infrastructure;
public static class ResearchAbstractUsagePolicy
{
    public sealed record Decision(string Status,bool PersistentStorageAllowed,bool TradingAuthorized);
    // Publication-level metadata alone does not establish rights to the abstract.
    public static Decision Evaluate(string? licenseUrl,bool abstractScopeVerified)
    {
        if(!abstractScopeVerified)return new("SCOPE_UNVERIFIED",false,false);
        if(!Uri.TryCreate(licenseUrl,UriKind.Absolute,out var uri)||uri.Scheme!="https"||
           !uri.IsDefaultPort||uri.UserInfo.Length!=0||uri.Query.Length!=0||uri.Fragment.Length!=0||
           !string.Equals(uri.IdnHost,"creativecommons.org",StringComparison.OrdinalIgnoreCase))
            return new("LICENSE_UNVERIFIED",false,false);
        return uri.AbsolutePath switch
        {
            "/publicdomain/zero/1.0/" => new("CC0_VERIFIED",true,false),
            "/licenses/by/4.0/" => new("ATTRIBUTION_REQUIRED",false,false),
            _ => new("LICENSE_REVIEW_REQUIRED",false,false)
        };
    }
}
