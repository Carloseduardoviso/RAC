using RAC.Application;

namespace RAC.Tests;

public sealed class RacSafetyDiagnosticsTests
{
    private static RacSafetyDiagnostics.Snapshot Snapshot(
        DateTimeOffset now) => new(
            PaperPresent:true, PaperRunning:true, PaperRiskLocked:false,
            LastPaperCycleUtc:now.AddSeconds(-30),
            LastMonitorSampleUtc:now.AddSeconds(-35),
            TestnetPresent:true, TestnetEnabled:false, TestnetEmergencyStopped:false,
            TestnetOrderCount:0, OpenPaperAlerts:0, PendingPolicyReviews:0,
            ObservedUtc:now);

    [Fact]
    public void Healthy_running_paper_and_disabled_testnet_produce_no_alerts()
    {
        var report = RacSafetyDiagnostics.Evaluate(Snapshot(DateTimeOffset.UtcNow));
        Assert.Equal("OK", report.Status);
        Assert.Empty(report.Incidents);
        Assert.False(report.RealOrdersEnabled);
        Assert.False(report.TestnetEnabled);
        Assert.Equal("READ_ONLY_SAFETY_DIAGNOSTICS", report.Mode);
    }

    [Fact]
    public void Planned_paper_pause_does_not_trigger_stale_cycle_warning()
    {
        var now = DateTimeOffset.UtcNow;
        var report = RacSafetyDiagnostics.Evaluate(Snapshot(now) with {
            PaperRunning = false, LastPaperCycleUtc = now.AddDays(-1)
        });
        Assert.DoesNotContain(report.Incidents, a => a.Code == "PAPER_CYCLE_STALE");
    }

    [Fact]
    public void Unexpected_testnet_activation_is_critical_but_not_authorized()
    {
        var report = RacSafetyDiagnostics.Evaluate(Snapshot(DateTimeOffset.UtcNow) with {
            TestnetEnabled = true
        });
        Assert.Equal("CRITICAL", report.Status);
        Assert.Contains(report.Incidents, a => a.Code == "TESTNET_UNEXPECTEDLY_ENABLED");
        Assert.False(report.RealOrdersEnabled);
    }

    [Fact]
    public void Missing_heartbeat_and_monitor_are_attention_incidents()
    {
        var report = RacSafetyDiagnostics.Evaluate(Snapshot(DateTimeOffset.UtcNow) with {
            LastPaperCycleUtc = null, LastMonitorSampleUtc = null
        });
        Assert.Equal("ATTENTION", report.Status);
        Assert.Contains(report.Incidents, a => a.Code == "PAPER_CYCLE_STALE");
        Assert.Contains(report.Incidents, a => a.Code == "MONITOR_SAMPLE_STALE");
    }

    [Fact]
    public void Existing_locks_and_incidents_are_reported_without_state_mutation()
    {
        var input = Snapshot(DateTimeOffset.UtcNow) with {
            PaperRiskLocked = true, OpenPaperAlerts = 2,
            PendingPolicyReviews = 3, TestnetOrderCount = 1
        };
        var result = RacSafetyDiagnostics.Evaluate(input);
        Assert.Equal("CRITICAL", result.Status);
        Assert.Contains(result.Incidents, a => a.Code == "PAPER_RISK_LOCK");
        Assert.Contains(result.Incidents, a => a.Code == "PAPER_MONITOR_ALERTS");
        Assert.Contains(result.Incidents, a => a.Code == "POLICY_REVIEWS_PENDING");
        Assert.Equal(3, result.PendingReviews);
        Assert.Equal(1, result.TestnetOrders);
        Assert.True(input.PaperRunning); // diagnostics cannot stop the PAPER engine
    }

    [Fact]
    public void Missing_persistent_state_fails_closed()
    {
        var result = RacSafetyDiagnostics.Evaluate(Snapshot(DateTimeOffset.UtcNow) with {
            PaperPresent=false, TestnetPresent=false
        });
        Assert.Equal("CRITICAL", result.Status);
        Assert.Contains(result.Incidents, a => a.Code == "PAPER_ACCOUNT_MISSING");
        Assert.Contains(result.Incidents, a => a.Code == "TESTNET_STATE_MISSING");
    }
}
