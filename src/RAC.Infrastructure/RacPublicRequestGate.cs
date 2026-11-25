namespace RAC.Infrastructure;

// Process-local coordination of anonymous market-data requests only.
public sealed class RacPublicRequestGate(TimeProvider clock)
{
    private readonly object sync=new();
    private readonly Dictionary<string,(long Started,TimeSpan Delay)> windows=new(StringComparer.OrdinalIgnoreCase);
    private static void Validate(string host)
    {
        if(host is not ("api.binance.com" or "testnet.binance.vision"))
            throw new ArgumentException("Unsupported public endpoint.");
    }
    public bool TryAcquire(string host,TimeSpan interval,out TimeSpan remaining)
    {
        Validate(host);
        if(interval<=TimeSpan.Zero || interval>TimeSpan.FromMinutes(1)) throw new ArgumentOutOfRangeException(nameof(interval));
        lock(sync)
        {
            var now=clock.GetTimestamp();
            if(windows.TryGetValue(host,out var window))
            {
                remaining=window.Delay-clock.GetElapsedTime(window.Started,now);
                if(remaining>TimeSpan.Zero) return false;
            }
            windows[host]=(now,interval);
            remaining=TimeSpan.Zero;
            return true;
        }
    }
    public void Defer(string host,TimeSpan delay)
    {
        Validate(host);
        if(delay<=TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delay));
        lock(sync)
        {
            var now=clock.GetTimestamp();
            if(windows.TryGetValue(host,out var window))
                delay=TimeSpan.FromTicks(Math.Max(delay.Ticks,(window.Delay-clock.GetElapsedTime(window.Started,now)).Ticks));
            windows[host]=(now,delay);
        }
    }
}
