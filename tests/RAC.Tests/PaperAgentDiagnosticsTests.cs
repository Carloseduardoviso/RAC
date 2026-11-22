using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class PaperAgentDiagnosticsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 20, 0, 0, TimeSpan.Zero);

    private static PaperAgentCursor Agent(DateTimeOffset candle, string status = "READY") => new()
    {
        Symbol = "BTCUSDT", Status = status, LastSignal = "AGUARDAR",
        LastCandleUtc = candle, Note = ""
    };

    [Fact]
    public void Fresh_ready_agent_is_healthy_even_without_trades()
    {
        var health = PaperAgentDiagnostics.Evaluate(Agent(Now.AddSeconds(-60)), Now);
        Assert.Equal("READY", health.Status);
        Assert.True(health.Healthy);
        Assert.Equal(60d, health.CandleAgeSeconds);
    }

    [Fact]
    public void Missing_blocked_stale_and_future_candles_are_distinguished()
    {
        Assert.Equal("MISSING", PaperAgentDiagnostics.Evaluate(null, Now).Status);
        Assert.Equal("BLOCKED", PaperAgentDiagnostics.Evaluate(Agent(Now.AddSeconds(-10), "ERROR"), Now).Status);
        Assert.Equal("STALE", PaperAgentDiagnostics.Evaluate(Agent(Now.AddSeconds(-241)), Now).Status);
        Assert.Equal("CLOCK_SKEW", PaperAgentDiagnostics.Evaluate(Agent(Now.AddSeconds(1)), Now).Status);
        Assert.All(new PaperAgentCursor?[] {null, Agent(Now.AddSeconds(-10), "ERROR"),
            Agent(Now.AddSeconds(-241)), Agent(Now.AddSeconds(1))},
            x => Assert.False(PaperAgentDiagnostics.Evaluate(x, Now).Healthy));
    }

    [Fact]
    public void Freshness_boundary_is_inclusive_but_stale_excludes_old_candles()
    {
        Assert.True(PaperAgentDiagnostics.Evaluate(Agent(Now.AddSeconds(-240)), Now).Healthy);
        Assert.False(PaperAgentDiagnostics.Evaluate(Agent(Now.AddSeconds(-241)), Now).Healthy);
    }
}
