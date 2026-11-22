namespace RAC.Application;

/// <summary>
/// Require repeated independent monitor polls before opening transient READY/candle
/// warnings. Critical risk, accounting and cycle incidents are never delayed.
/// In-memory candidates are discarded on restart; persisted incidents are retained.
/// </summary>
public sealed class PaperAlertDebounce
{
    private readonly Dictionary<string, DateTimeOffset> _firstSeen = new(StringComparer.Ordinal);

    public bool ShouldOpen(string code, DateTimeOffset now)
    {
        if (code != "AGENTS_NOT_READY" && !code.StartsWith("AGENT_", StringComparison.Ordinal))
            return true;
        if (!_firstSeen.TryGetValue(code, out var since))
        {
            _firstSeen[code] = now;
            return false;
        }
        // A failed poll cannot be interpreted as persistent continuous trouble.
        if (now < since || now - since > TimeSpan.FromMinutes(3))
        {
            _firstSeen[code] = now;
            return false;
        }
        return now - since >= TimeSpan.FromSeconds(50);
    }

    public void ForgetResolved(IEnumerable<string> currentlyObserved)
    {
        var seen = currentlyObserved.ToHashSet(StringComparer.Ordinal);
        foreach (var code in _firstSeen.Keys.Where(code => !seen.Contains(code)).ToArray())
            _firstSeen.Remove(code);
    }
}
