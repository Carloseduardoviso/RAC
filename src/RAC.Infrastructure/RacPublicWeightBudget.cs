namespace RAC.Infrastructure;

// Conservative internal budget, not a claim about the exchange's current limits.
public sealed class RacPublicWeightBudget(TimeProvider clock,int capacity=1200)
{
    private readonly object sync=new();
    private readonly Dictionary<string,Queue<(long Time,int Weight)>> history=new();
    public bool TryReserve(string host,int weight)
    {
        if(host is not ("api.binance.com" or "testnet.binance.vision") || weight<=0 || capacity<=0)
            throw new ArgumentException("Invalid public budget request.");
        lock(sync)
        {
            var now=clock.GetTimestamp();
            if(!history.TryGetValue(host,out var queue)) history[host]=queue=new();
            while(queue.TryPeek(out var item) && clock.GetElapsedTime(item.Time,now)>=TimeSpan.FromMinutes(1))
                queue.Dequeue();
            if(queue.Sum(x=>(long)x.Weight)+weight>capacity) return false;
            queue.Enqueue((now,weight));
            return true;
        }
    }
    public static int Estimate(Uri uri)=>uri.AbsolutePath switch
    {
        "/api/v3/time"=>1,
        "/api/v3/exchangeInfo"=>20,
        "/api/v3/ticker/24hr"=>80,
        "/api/v3/depth"=>250,
        _=>throw new InvalidOperationException("Unknown public request weight.")
    };
}
