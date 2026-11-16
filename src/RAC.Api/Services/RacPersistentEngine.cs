using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RAC.Api.Models;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Api.Services;

// Serializes all mutations and saves a complete checkpoint plus a searchable ledger.
// Single application instance only: SQL rowversion rejects writes from a stale second instance.
public sealed class RacPersistentEngine
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly IDbContextFactory<RacDbContext> _database;
    private readonly RacSimulator _simulator;
    private readonly ILogger<RacPersistentEngine> _logger;
    private byte[] _checkpointVersion;
    private bool _storageFault;

    public RacPersistentEngine(IDbContextFactory<RacDbContext> database,
        RacSimulator simulator, ILogger<RacPersistentEngine> logger)
    {
        _database = database;
        _simulator = simulator;
        _logger = logger;

        using var db = _database.CreateDbContext();
        if (db.Database.GetPendingMigrations().Any())
            throw new InvalidOperationException("Há migrations RAC pendentes. Atualize o banco antes de iniciar a API.");

        var checkpoint = db.SimulationCheckpoints.SingleOrDefault(x => x.Id == 1);
        if (checkpoint is null)
        {
            var state = _simulator.ExportState();
            checkpoint = new SimulationCheckpoint
            {
                StateJson = JsonSerializer.Serialize(state with { PendingEvents = [] }),
                Tick = 0
            };
            db.SimulationCheckpoints.Add(checkpoint);
            db.SaveChanges();
            _logger.LogInformation("Checkpoint de simulação RAC criado no SQL Server.");
        }
        else
        {
            var state = JsonSerializer.Deserialize<SimulationState>(checkpoint.StateJson)
                ?? throw new InvalidOperationException("Checkpoint RAC está vazio.");
            _simulator.RestoreState(state); // Always starts paused after a restart.
            _logger.LogInformation("RAC restaurado do SQL Server no tick {Tick}; inicialização pausada.", state.Tick);
        }

        _checkpointVersion = checkpoint.RowVersion.ToArray();

        // A previously persisted risk lock always takes precedence over a checkpoint.
        var savedLock = db.RiskLocks.AsNoTracking().Any(x => x.Active);
        if (savedLock && !_simulator.Snapshot().RiskLocked)
            throw new InvalidOperationException("Bloqueio ativo encontrado no banco sem checkpoint correspondente. Revisão manual obrigatória.");
    }

    public async Task<RacState> SnapshotAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { return _simulator.Snapshot(); }
        finally { _gate.Release(); }
    }

    public Task<RacState> StartAsync(CancellationToken ct, string? actor = null) => ChangeAsync("START", ct, actor);
    public Task<RacState> PauseAsync(CancellationToken ct, string? actor = null) => ChangeAsync("PAUSE", ct, actor);
    public Task<RacState> StepAsync(CancellationToken ct, string? actor = null) => ChangeAsync("STEP", ct, actor);
    public Task<RacState> ResetAsync(CancellationToken ct, string? actor = null) => ChangeAsync("RESET", ct, actor);
    public Task<RacState> TickAsync(CancellationToken ct) => ChangeAsync("TICK", ct);

    public async Task<RacState> ReviewAndResetAsync(string reviewer, string justification, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(justification) || justification.Trim().Length < 20 ||
            justification.Length > 500)
            throw new ArgumentException("Informe uma justificativa entre 20 e 500 caracteres.", nameof(justification));

        await _gate.WaitAsync(ct);
        try
        {
            if (_storageFault)
                throw new InvalidOperationException("Persistência indisponível; revisão não autorizada.");
            if (!_simulator.Snapshot().RiskLocked)
                throw new InvalidOperationException("Não existe bloqueio de risco a liberar.");

            var before = _simulator.ExportState();
            _simulator.ResetAfterAuthorizedReview();
            var state = _simulator.ExportState();
            try
            {
                await SaveAsync(state, "REVIEW_RESET", ct, reviewer, justification.Trim());
                _simulator.ConfirmSavedEvents();
            }
            catch (Exception exception)
            {
                _logger.LogCritical(exception, "Falha na revisão de risco RAC; execução bloqueada.");
                _simulator.RestoreState(before);
                _simulator.StorageFailure();
                _storageFault = true;
                throw new InvalidOperationException("Não foi possível registrar a revisão; cenário bloqueado.", exception);
            }
            return _simulator.Snapshot();
        }
        finally { _gate.Release(); }
    }

    private async Task<RacState> ChangeAsync(string action, CancellationToken ct, string? actor = null)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_storageFault)
                throw new InvalidOperationException("Persistência indisponível. Corrija o SQL Server e reinicie a API.");
            if (action == "TICK" && !_simulator.Snapshot().Running)
                return _simulator.Snapshot();

            // Fail closed: a risk lock cannot be reset through an unauthenticated HTTP action.
            if (_simulator.Snapshot().RiskLocked && action is "START" or "RESET")
                throw new InvalidOperationException("Carlos bloqueou o cenário. Revisão autorizada é necessária.");

            var before = _simulator.ExportState();
            switch (action)
            {
                case "START": _simulator.Start(); break;
                case "PAUSE": _simulator.Pause(); break;
                case "TICK":
                case "STEP": _simulator.Tick(); break;
                case "RESET": _simulator.Reset(); break;
                default: throw new ArgumentOutOfRangeException(nameof(action));
            }
            var state = _simulator.ExportState();
            try
            {
                await SaveAsync(state, action, ct, actor);
                _simulator.ConfirmSavedEvents();
            }
            catch (Exception exception)
            {
                _logger.LogCritical(exception, "Falha ao persistir {Action}; negociação simulada bloqueada.", action);
                _simulator.RestoreState(before);
                _simulator.StorageFailure();
                _storageFault = true;
                throw new InvalidOperationException("Falha ao salvar a simulação. Cenário bloqueado.", exception);
            }
            return _simulator.Snapshot();
        }
        finally { _gate.Release(); }
    }

    private async Task SaveAsync(SimulationState state, string action, CancellationToken ct,
        string? actor = null, string? justification = null)
    {
        await using var db = await _database.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var checkpoint = new SimulationCheckpoint
        {
            Id = 1,
            StateJson = JsonSerializer.Serialize(state with { PendingEvents = [] }),
            Tick = state.Tick,
            RowVersion = _checkpointVersion,
            UpdatedUtc = DateTimeOffset.UtcNow
        };
        db.Attach(checkpoint);
        db.Entry(checkpoint).Property(x => x.RowVersion).OriginalValue = _checkpointVersion;
        db.Entry(checkpoint).Property(x => x.StateJson).IsModified = true;
        db.Entry(checkpoint).Property(x => x.Tick).IsModified = true;
        db.Entry(checkpoint).Property(x => x.UpdatedUtc).IsModified = true;

        if (action is "TICK" or "STEP" or "RESET")
        {
            var current = _simulator.Snapshot();
            db.EquitySnapshots.Add(new EquitySnapshot
            {
                EquityBrl = current.Equity,
                TotalContributionsBrl = current.TotalContributionsBrl,
                DrawdownPercent = current.PeakDrawdownPct
            });
        }

        foreach (var change in state.PendingEvents)
            await SaveEventAsync(db, state, change, ct);

        if (action != "TICK")
        {
            db.AuditEvents.Add(new AuditEvent
            {
                EventType = "SIM_CONTROL_" + action,
                Description = $"Comando {action} por {actor ?? "sistema"} no tick {state.Tick}; sessão {state.SessionId:N}"
            });
        }

        if (action == "REVIEW_RESET")
        {
            var activeLocks = await db.RiskLocks.Where(x => x.Active).ToArrayAsync(ct);
            if (activeLocks.Length == 0)
                throw new InvalidOperationException("O SQL Server não possui bloqueio ativo para esta revisão.");
            foreach (var riskLock in activeLocks)
            {
                riskLock.Active = false;
                riskLock.ReleasedUtc = DateTimeOffset.UtcNow;
            }
            db.AuditEvents.Add(new AuditEvent
            {
                EventType = "RISK_REVIEW_APPROVED",
                Description = $"Revisor: {actor}; nova sessão: {state.SessionId:N}; justificativa: {justification}"
            });
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        _checkpointVersion = checkpoint.RowVersion.ToArray();
    }

    private static async Task SaveEventAsync(RacDbContext db, SimulationState state,
        SimEvent change, CancellationToken ct)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            EventType = "SIM_" + change.Kind,
            Description = $"Sessão {state.SessionId:N}, tick {change.Tick}, símbolo {change.Symbol ?? "-"}: {change.Detail}"
        });

        switch (change.Kind)
        {
            case "CONTRIBUTION":
                db.Contributions.Add(new Contribution { AmountBrl = change.AmountBrl, IsVirtual = true });
                break;
            case "RISK_LOCK":
                db.RiskLocks.Add(new RiskLock { Reason = state.RiskMessage, Active = true });
                break;
            case "OPEN":
                await SaveOrderAsync(db, state, change, true, ct);
                break;
            case "CLOSE":
                await SaveOrderAsync(db, state, change, false, ct);
                break;
        }
    }

    private static async Task SaveOrderAsync(RacDbContext db, SimulationState state,
        SimEvent change, bool opening, CancellationToken ct)
    {
        if (change.PositionId is null || change.Symbol is null)
            throw new InvalidOperationException("Evento de posição simulada inválido.");

        var key = $"SIM-{state.SessionId:N}-{change.PositionId}";
        if (opening)
        {
            var opened = state.Positions.SingleOrDefault(x => x.Id == change.PositionId)
                ?? throw new InvalidOperationException("Abertura não encontrada no checkpoint.");
            db.Positions.Add(new RAC.Domain.Position
            {
                SimulationKey = key, Symbol = opened.Symbol, Quantity = opened.Qty,
                AverageEntry = opened.Entry, StopPrice = opened.Stop, IsOpen = true,
                Mode = OperationMode.Simulation
            });
        }
        else
        {
            var position = await db.Positions.SingleOrDefaultAsync(x => x.SimulationKey == key, ct)
                ?? throw new InvalidOperationException("Posição simulada não localizada no SQL Server.");
            position.IsOpen = false;
        }

        var order = new TradeOrder
        {
            ClientOrderId = key + (opening ? "-BUY" : "-SELL"),
            Symbol = change.Symbol,
            Side = opening ? "BUY" : "SELL",
            Quantity = change.Quantity,
            Price = change.Price,
            Status = OrderStatus.Filled,
            Mode = OperationMode.Simulation
        };
        db.Orders.Add(order);
        db.Executions.Add(new TradeExecution
        {
            Order = order, Quantity = change.Quantity, Price = change.Price, Fee = change.Fee
        });
    }
}
