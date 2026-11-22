using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class PaperCycleDiagnosticsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 20, 0, 0, TimeSpan.Zero);
    private static PaperAccount Account() => new() {
        Running = true, UpdatedUtc = Now.AddMinutes(-10),
        LastCycleUtc = Now.AddSeconds(-50), RiskLocked = false
    };

    [Fact]
    public void Running_worker_is_reported_as_healthy()
    {
        var state = PaperCycleDiagnostics.Evaluate(Account(), Now);
        Assert.Equal("RUNNING", state.Status);
        Assert.Equal(50d, state.LastCycleAgeSeconds);
    }

    [Fact]
    public void Recent_start_is_not_misclassified_as_a_stall()
    {
        var account = Account();
        account.LastCycleUtc = Now.AddHours(-1);
        account.UpdatedUtc = Now.AddSeconds(-15);
        Assert.Equal("STARTING", PaperCycleDiagnostics.Evaluate(account, Now).Status);
        account.LastCycleUtc = null;
        Assert.Equal("STARTING", PaperCycleDiagnostics.Evaluate(account, Now).Status);
    }

    [Fact]
    public void Stalled_cycle_and_future_clock_are_reported_explicitly()
    {
        var account = Account();
        account.LastCycleUtc = Now.AddMinutes(-9);
        Assert.Equal("STALLED", PaperCycleDiagnostics.Evaluate(account, Now).Status);
        account.LastCycleUtc = Now.AddMinutes(5);
        Assert.Equal("CLOCK_SKEW", PaperCycleDiagnostics.Evaluate(account, Now).Status);
    }

    [Fact]
    public void Paused_and_locked_are_not_described_as_running()
    {
        var account = Account();
        account.Running = false;
        Assert.Equal("PAUSED", PaperCycleDiagnostics.Evaluate(account, Now).Status);
        account.RiskLocked = true;
        Assert.Equal("RISK_LOCKED", PaperCycleDiagnostics.Evaluate(account, Now).Status);
        Assert.Equal("UNINITIALIZED", PaperCycleDiagnostics.Evaluate(null, Now).Status);
    }

    [Fact]
    public void Short_agent_outage_is_suppressed_but_persistent_outage_is_reported()
    {
        var filter = new PaperAlertDebounce();
        Assert.False(filter.ShouldOpen("AGENT_BTCUSDT", Now));
        Assert.False(filter.ShouldOpen("AGENT_BTCUSDT", Now.AddSeconds(30)));
        Assert.True(filter.ShouldOpen("AGENT_BTCUSDT", Now.AddSeconds(60)));
        filter.ForgetResolved(Array.Empty<string>());
        Assert.False(filter.ShouldOpen("AGENT_BTCUSDT", Now.AddSeconds(90)));
        Assert.True(filter.ShouldOpen("PAPER_CYCLE_STALE", Now));
        Assert.True(filter.ShouldOpen("RISK_LOCKED", Now));
    }

    [Fact]
    public void Missed_monitor_samples_do_not_count_as_consecutive_evidence()
    {
        var filter = new PaperAlertDebounce();
        Assert.False(filter.ShouldOpen("AGENTS_NOT_READY", Now));
        Assert.False(filter.ShouldOpen("AGENTS_NOT_READY", Now.AddMinutes(4)));
        Assert.True(filter.ShouldOpen("AGENTS_NOT_READY", Now.AddMinutes(5)));
    }
}
