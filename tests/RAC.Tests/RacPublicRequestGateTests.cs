using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class RacPublicRequestGateTests
{
    private sealed class Clock:TimeProvider
    {
        public long Tick;
        public override long TimestampFrequency=>TimeSpan.TicksPerSecond;
        public override long GetTimestamp()=>Interlocked.Read(ref Tick);
        public void Advance(TimeSpan span)=>Interlocked.Add(ref Tick,span.Ticks);
    }
    [Fact]
    public async Task ConcurrentRequestsShareCooldownWithoutBlockingSeparateEnvironment()
    {
        var clock=new Clock();
        var gate=new RacPublicRequestGate(clock);
        var interval=TimeSpan.FromSeconds(2);
        var results=await Task.WhenAll(Enumerable.Range(0,20).Select(_=>Task.Run(()=>gate.TryAcquire("api.binance.com",interval,out var remaining))));
        Assert.Single(results,x=>x);
        Assert.True(gate.TryAcquire("testnet.binance.vision",interval,out _));
        gate.Defer("api.binance.com",TimeSpan.FromMinutes(5));
        gate.Defer("api.binance.com",interval);
        clock.Advance(interval);
        Assert.False(gate.TryAcquire("api.binance.com",interval,out var wait));
        Assert.Equal(TimeSpan.FromMinutes(5)-interval,wait);
        Assert.True(gate.TryAcquire("testnet.binance.vision",interval,out _));
        clock.Advance(wait);
        Assert.True(gate.TryAcquire("api.binance.com",interval,out _));
        Assert.Throws<ArgumentException>(()=>gate.TryAcquire("example.com",interval,out _));
    }
}
