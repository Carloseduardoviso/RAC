using RAC.Application;
namespace RAC.Infrastructure;

// Discovery metadata is not proof of publisher credibility.
public static class ResearchDiscoveryEvidence
{
    public sealed record Observation(string Provider,string Host,bool ValidHttpsReference,bool HostMatches,bool ProviderRecognized,ResearchSourceReliability.Evidence Evidence);
    public static Observation Inspect(string host,string url,string provider)
    {
        var valid=Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&
            uri.HostNameType==UriHostNameType.Dns&&uri.IsDefaultPort&&string.IsNullOrEmpty(uri.UserInfo);
        var matches=valid&&string.Equals(uri!.IdnHost.TrimEnd('.'),host,StringComparison.OrdinalIgnoreCase);
        var recognized=string.Equals(provider,"crossref",StringComparison.Ordinal);
        // A matching URL is only a reference; provenance remains unverified.
        return new(provider,host,valid,matches,recognized,new(null,null,null,null,null,false));
    }
}
