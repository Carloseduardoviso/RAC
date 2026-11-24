namespace RAC.Application;

// Discovery is not approval. Candidates cannot influence trading.
public sealed class ResearchSourceCandidates
{
    public sealed record Candidate(string Host,string EvidenceUrl,string DiscoveredFrom,DateTimeOffset DiscoveredUtc);
    private readonly Dictionary<string,Candidate> candidates=new(StringComparer.OrdinalIgnoreCase);
    private readonly object gate=new();
    public bool Observe(string url,string discoveredFrom,DateTimeOffset observedUtc)
    {
        if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||
            uri.HostNameType!=UriHostNameType.Dns||!uri.Host.Contains('.')||
            !string.IsNullOrEmpty(uri.UserInfo)||!uri.IsDefaultPort||
            url.Length>2048||string.IsNullOrWhiteSpace(discoveredFrom)||discoveredFrom.Length>100)
            return false;
        var host=uri.IdnHost.TrimEnd('.');
        if(FinancialSourceCatalog.Sources.Any(x=>new Uri(x.HomePage).IdnHost.Equals(host,StringComparison.OrdinalIgnoreCase)))
            return false;
        lock(gate)
        {
            if(candidates.Count>=1000)return false;
            return candidates.TryAdd(host,new(host,uri.AbsoluteUri,discoveredFrom,observedUtc));
        }
    }
    public IReadOnlyList<Candidate> Snapshot()
    {
        lock(gate)return candidates.Values.OrderBy(x=>x.Host,StringComparer.Ordinal).ToArray();
    }
}
