namespace RAC.Application;
public interface ILucasExchangeProbe
{
    string Exchange {get;}
    string Environment {get;}
    Task<LucasExchangeObservation> ReadAsync(CancellationToken ct);
}
public sealed class LucasExchangeProbeRegistry
{
    private readonly Dictionary<(string Exchange,string Environment),ILucasExchangeProbe> probes=new();
    public LucasExchangeProbeRegistry(IEnumerable<ILucasExchangeProbe> providers)
    {
        foreach(var provider in providers)
        {
            if(string.IsNullOrWhiteSpace(provider.Exchange) || provider.Environment is not ("PUBLIC" or "TESTNET") ||
                !probes.TryAdd((provider.Exchange,provider.Environment),provider))
                throw new ArgumentException("Invalid or duplicate exchange probe.");
        }
        if(probes.Count==0 || probes.Count>20) throw new ArgumentException("Invalid probe count.");
    }
    public IReadOnlyList<(string Exchange,string Environment)> Available=>probes.Keys.OrderBy(x=>x.Exchange,StringComparer.Ordinal).ThenBy(x=>x.Environment,StringComparer.Ordinal).ToArray();
    public async Task<LucasExchangeObservation> ReadAsync(string exchange,string environment,CancellationToken ct)
    {
        if(!probes.TryGetValue((exchange,environment),out var probe))
            throw new InvalidOperationException("Exchange environment is not registered.");
        var result=await probe.ReadAsync(ct);
        if(result.Exchange!=exchange || result.Environment!=environment)
            throw new InvalidOperationException("Exchange observation identity mismatch.");
        return result;
    }
}
