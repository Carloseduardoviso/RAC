using RAC.Application;
namespace RAC.Tests;
public sealed class LucasExchangeProbeRegistryTests
{
    private sealed class Probe(string exchange,string environment,string? returnedExchange=null):ILucasExchangeProbe
    {
        public string Exchange=>exchange;
        public string Environment=>environment;
        public Task<LucasExchangeObservation> ReadAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var now=DateTimeOffset.UtcNow;
            return Task.FromResult(new LucasExchangeObservation(returnedExchange??exchange,environment,now,now,now,200));
        }
    }
    [Fact]
    public async Task RoutesOnlyRegisteredReadOnlyEnvironments()
    {
        var spot=new Probe("BINANCE","PUBLIC");
        var testnet=new Probe("BINANCE","TESTNET");
        var registry=new LucasExchangeProbeRegistry(new[]{spot,testnet,new Probe("EXAMPLE","PUBLIC")});
        Assert.Equal(3,registry.Available.Count);
        Assert.Equal("TESTNET",(await registry.ReadAsync("BINANCE","TESTNET",default)).Environment);
        Assert.Equal("EXAMPLE",(await registry.ReadAsync("EXAMPLE","PUBLIC",default)).Exchange);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>registry.ReadAsync("BINANCE","LIVE",default));
        Assert.Throws<ArgumentException>(()=>new LucasExchangeProbeRegistry(new[]{spot,spot}));
        var mismatch=new LucasExchangeProbeRegistry(new[]{new Probe("BINANCE","PUBLIC","OTHER")});
        await Assert.ThrowsAsync<InvalidOperationException>(()=>mismatch.ReadAsync("BINANCE","PUBLIC",default));
    }
}
