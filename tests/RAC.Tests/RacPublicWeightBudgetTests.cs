using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class RacPublicWeightBudgetTests
{
    private sealed class Clock:TimeProvider
    {
        public long Tick;
        public override long TimestampFrequency=>TimeSpan.TicksPerSecond;
        public override long GetTimestamp()=>Interlocked.Read(ref Tick);
    }
    [Fact]
    public async Task ConcurrentWeightedReservationsRespectRollingBudget()
    {
        var clock=new Clock();
        var budget=new RacPublicWeightBudget(clock,100);
        var results=await Task.WhenAll(Enumerable.Range(0,20).Select(_=>Task.Run(()=>budget.TryReserve("api.binance.com",20))));
        Assert.Equal(5,results.Count(x=>x));
        Assert.False(budget.TryReserve("api.binance.com",1));
        Assert.True(budget.TryReserve("testnet.binance.vision",100));
        clock.Tick=TimeSpan.FromSeconds(59).Ticks;
        Assert.False(budget.TryReserve("api.binance.com",1));
        clock.Tick=TimeSpan.FromMinutes(1).Ticks;
        Assert.True(budget.TryReserve("api.binance.com",100));
    }
}
