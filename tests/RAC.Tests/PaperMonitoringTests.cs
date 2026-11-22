using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

public sealed class PaperMonitoringTests
{
    private static readonly DateTimeOffset Now = new(2026,11,22,18,0,0,TimeSpan.Zero);
    private static PaperAccount Account(bool running = true) => new()
    {
        Id = 1, Running = running, RiskLocked = false,
        LastEquityBrl = 100m, CashBrl = 100m,
        PeakEquityBrl = 100m, DailyStartEquityBrl = 100m,
        LastCycleUtc = Now.AddSeconds(-45),
        UpdatedUtc = Now.AddMinutes(-6)
    };
    private static PaperAgentCursor[] HealthyAgents() =>
        RacMarketSymbols.Supported.Select(x => new PaperAgentCursor
        {
            Symbol = x, LastCandleUtc = Now.AddMinutes(-1),
            LastSignal = "AGUARDAR", Status = "READY"
        }).ToArray();

    [Fact]
    public void Healthy_simulation_has_no_alerts()
    {
        Assert.Empty(PaperMonitoringPolicy.Evaluate(Account(), HealthyAgents(),
            Now.AddMinutes(-1), Now));
    }

    [Fact]
    public void Paused_restart_is_not_misdiagnosed_as_a_stuck_worker()
    {
        var restored = Account(false);
        restored.LastCycleUtc = Now.AddHours(-10);
        Assert.Empty(PaperMonitoringPolicy.Evaluate(restored, [], null, Now));
    }

    [Fact]
    public void A_fresh_manual_start_does_not_trigger_false_worker_alert()
    {
        var account = Account();
        account.LastCycleUtc = null;
        account.UpdatedUtc = Now.AddSeconds(-20);
        var alerts = PaperMonitoringPolicy.Evaluate(
            account, HealthyAgents(), Now.AddMinutes(-1), Now);
        Assert.DoesNotContain("PAPER_CYCLE_STALE", alerts.Keys);
    }

    [Fact]
    public void Worker_stall_creates_critical_alert_without_touching_risk_lock()
    {
        var account = Account();
        account.LastCycleUtc = Now.AddMinutes(-8);
        var alerts = PaperMonitoringPolicy.Evaluate(account, HealthyAgents(), Now.AddMinutes(-1), Now);
        Assert.Equal("CRITICAL", alerts["PAPER_CYCLE_STALE"].Severity);
        Assert.False(account.RiskLocked);
    }

    [Fact]
    public void Market_outage_and_missing_agents_are_reported_independently()
    {
        var alerts = PaperMonitoringPolicy.Evaluate(Account(), HealthyAgents().Take(10).ToArray(),
            Now.AddMinutes(-9), Now);
        Assert.Contains("MARKET_STALE", alerts.Keys);
        Assert.Contains("AGENTS_NOT_READY", alerts.Keys);
    }

    [Fact]
    public void Recovery_closes_derived_alerts_without_unlocking_a_risk_stop()
    {
        var account = Account();
        account.LastCycleUtc = Now.AddMinutes(-10);
        Assert.Contains("PAPER_CYCLE_STALE",
            PaperMonitoringPolicy.Evaluate(account, HealthyAgents(), Now.AddMinutes(-2), Now).Keys);
        account.LastCycleUtc = Now.AddSeconds(-10);
        Assert.DoesNotContain("PAPER_CYCLE_STALE",
            PaperMonitoringPolicy.Evaluate(account, HealthyAgents(), Now.AddSeconds(-20), Now).Keys);
        account.RiskLocked = true;
        Assert.Contains("RISK_LOCKED",
            PaperMonitoringPolicy.Evaluate(account, HealthyAgents(), Now.AddSeconds(-20), Now).Keys);
        Assert.True(account.RiskLocked);
    }

    [Fact]
    public void Monitoring_identifies_daily_loss_and_drawdown()
    {
        var account = Account();
        account.LastEquityBrl = 89m;
        var alerts = PaperMonitoringPolicy.Evaluate(account, HealthyAgents(), Now.AddMinutes(-1), Now);
        Assert.Contains("DAILY_LOSS_LIMIT", alerts.Keys);
        Assert.Contains("DRAWDOWN_LIMIT", alerts.Keys);
    }

    [Fact]
    public void Monitoring_has_persisted_unique_audit_per_symbol_and_candle()
    {
        using var db = new RacDbContext(new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer("Server=(local);Database=MetadataOnly;Integrated Security=True;TrustServerCertificate=True")
            .Options);
        Assert.NotNull(db.Model.FindEntityType(typeof(PaperMonitorSample)));
        Assert.NotNull(db.Model.FindEntityType(typeof(PaperMonitorAlert)));
        var audit = db.Model.FindEntityType(typeof(PaperDecisionAudit));
        Assert.NotNull(audit);
        Assert.Contains(audit.GetIndexes(), x => x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual(
                [nameof(PaperDecisionAudit.Symbol), nameof(PaperDecisionAudit.CandleClosedUtc)]));
    }
}
