using System.Text.Json;
using RAC.Api.Models;
using RAC.Api.Services;

namespace RAC.Tests;

public sealed class SimulatorCheckpointTests
{
    [Fact]
    public void Recovering_checkpoint_keeps_equity_and_pauses_running_simulation()
    {
        var original = new RacSimulator(new RiskPolicy());
        original.Tick();
        original.Start();
        var json = JsonSerializer.Serialize(original.ExportState());
        var checkpoint = JsonSerializer.Deserialize<SimulationState>(json);
        Assert.NotNull(checkpoint);

        var recovered = new RacSimulator(new RiskPolicy());
        recovered.RestoreState(checkpoint);
        Assert.Equal(original.Snapshot().Tick, recovered.Snapshot().Tick);
        Assert.Equal(original.Snapshot().Equity, recovered.Snapshot().Equity);
        Assert.Equal(original.Snapshot().Agents.Count, recovered.Snapshot().Agents.Count);
        Assert.False(recovered.Snapshot().Running);
    }

    [Fact]
    public void Risk_lock_cannot_be_cleared_by_start_or_reset()
    {
        var simulator = new RacSimulator(new RiskPolicy());
        var state = simulator.ExportState() with
        {
            Running = true,
            RiskLocked = true,
            RiskMessage = "Carlos: bloqueio persistido para revisão."
        };
        simulator.RestoreState(state);
        simulator.Start();
        simulator.Tick();

        Assert.True(simulator.Snapshot().RiskLocked);
        Assert.False(simulator.Snapshot().Running);
        Assert.Equal(0, simulator.Snapshot().Tick);
        Assert.Throws<InvalidOperationException>(() => simulator.Reset());
    }

    [Fact]
    public void Legacy_checkpoint_with_three_daily_entries_restores_without_resetting_history()
    {
        var current = new RacSimulator(new RiskPolicy(MaxEntriesPerDay: 0));
        current.Tick();
        var saved = current.ExportState();
        var legacy = saved with { Policy = saved.Policy with { MaxEntriesPerDay = 3 } };
        var restored = new RacSimulator(new RiskPolicy(MaxEntriesPerDay: 0));
        restored.RestoreState(legacy);
        Assert.Equal(saved.SessionId, restored.ExportState().SessionId);
        Assert.Equal(saved.Tick, restored.ExportState().Tick);
        Assert.Equal(0, restored.ExportState().Policy.MaxEntriesPerDay);
        Assert.Equal(saved.Cash, restored.ExportState().Cash);
        Assert.Throws<InvalidOperationException>(() => restored.RestoreState(legacy with {
            Policy = legacy.Policy with { MaxDailyLossPct = 2m }
        }));
    }

    [Fact]
    public void Checkpoint_rejects_modified_policy()
    {
        var simulator = new RacSimulator(new RiskPolicy());
        var changed = simulator.ExportState() with
        {
            Policy = new RiskPolicy(MaxDailyLossPct: 2m)
        };
        Assert.Throws<InvalidOperationException>(() => simulator.RestoreState(changed));
    }
}
