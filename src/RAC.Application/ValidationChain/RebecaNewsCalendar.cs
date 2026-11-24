namespace RAC.Application;

// Observational calendar cache. A missing or expired snapshot is not evidence of safety.
public sealed class RebecaNewsCalendar : IRacNewsCalendar
{
    public sealed record Item(string Symbol, RacNewsEvent Event);
    private sealed record Snapshot(DateTimeOffset ObservedUtc, Item[] Items);
    private Snapshot? snapshot;
    public void Publish(DateTimeOffset observedUtc, IEnumerable<Item> items)
    {
        var copy=items.ToArray();
        if(copy.Length>1000 || copy.Any(x=>string.IsNullOrWhiteSpace(x.Symbol) ||
            string.IsNullOrWhiteSpace(x.Event.Title) || x.Event.Title.Length>200 ||
            x.Event.Impact is not ("high" or "medium" or "low")))
            throw new ArgumentException("Invalid news snapshot.");
        lock(this)
        {
            if(snapshot is not null && observedUtc<=snapshot.ObservedUtc)
                throw new InvalidOperationException("Non-monotonic news snapshot.");
            Volatile.Write(ref snapshot,new Snapshot(observedUtc,copy));
        }
    }
    public IReadOnlyList<RacNewsEvent>? EventsAround(string symbol,DateTimeOffset nowUtc)
    {
        var current=Volatile.Read(ref snapshot);
        if(current is null || current.ObservedUtc>nowUtc ||
           nowUtc-current.ObservedUtc>TimeSpan.FromMinutes(5))return null;
        return current.Items.Where(x=>x.Symbol==symbol || x.Symbol=="ALL")
            .Select(x=>x.Event).ToArray();
    }
}
