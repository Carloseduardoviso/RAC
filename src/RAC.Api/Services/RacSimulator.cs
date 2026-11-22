using RAC.Api.Models;
using System.Security.Cryptography;

namespace RAC.Api.Services;

public sealed class RacSimulator
{
    private static readonly (string Symbol, decimal Price, decimal Vol)[] Markets =
    [
        ("BTCUSDT", 90000m, .009m), ("ETHUSDT", 3200m, .010m), ("SOLUSDT", 155m, .016m),
        ("BNBUSDT", 600m, .008m), ("XRPUSDT", 2m, .017m), ("ADAUSDT", .60m, .019m),
        ("DOGEUSDT", .20m, .024m), ("LINKUSDT", 17m, .013m), ("AVAXUSDT", 32m, .018m),
        ("LTCUSDT", 90m, .012m), ("DOTUSDT", 5m, .017m), ("TRXUSDT", .20m, .011m)
    ];
    private sealed class Trader
    {
        public required string Symbol { get; init; }
        public decimal Price { get; set; }
        public decimal Previous { get; set; }
        public decimal Vol { get; init; }
        public Queue<decimal> History { get; } = new();
        public string Signal { get; set; } = "AGUARDAR";
        public int Wins { get; set; }
        public int Losses { get; set; }
        public decimal RealizedPnl { get; set; }
    }
    private sealed record Position(long Id, string Symbol, decimal Entry, decimal Qty, decimal Stop, decimal Target,
        decimal EntryCostBrl, decimal EntryFeeBrl, int OpenedTick);
    private readonly object _gate = new();
    private readonly Dictionary<string, Trader> _traders = new();
    private readonly List<Position> _positions = new();
    private readonly List<ClosedTrade> _trades = new();
    private readonly List<decimal> _curve = new();
    private readonly RiskPolicy _policy;
    private readonly List<SimEvent> _pendingEvents = new();
    private Guid _sessionId = Guid.NewGuid();
    private static decimal RandomUnit() => RandomNumberGenerator.GetInt32(0, 1000000) / 1000000m;
    private decimal _cash, _startDayEquity, _totalContributions, _adjustedPeak;
    private int _contributedMonths;
    private int _tick, _entriesToday;
    private long _id;
    private bool _running, _riskLocked;
    private string _riskMessage = "Aguardando simulação. Notícias externas não conectadas; ordens reais bloqueadas.";
    public RacSimulator(RiskPolicy policy)
    {
        _policy = policy;
        if (_policy.ReferenceCapitalBrl <= 0 || _policy.MonthlyContributionBrl < 0 ||
            _policy.ReinvestmentPct != 100 || _policy.MaxCumulativeDrawdownPct <= 0 ||
            _policy.MaxCumulativeDrawdownPct > 100 || _policy.MaxRiskPerTradePct <= 0 ||
            _policy.MaxDailyLossPct <= 0 || _policy.MaxAllocationPerPositionPct <= 0 ||
            _policy.MaxTotalAllocationPct <= 0 || _policy.MaxOpenPositions < 1 ||
            _policy.MaxEntriesPerDay < 0 || _policy.SimulatedTicksPerDay < 1 ||
            _policy.SimulatedDaysPerMonth < 1 || _policy.IllustrativeFeePctPerSide < 0 ||
            _policy.AssumedBrlPerUsdt <= 0)
            throw new InvalidOperationException("Politica RAC invalida; simulação interrompida.");
        Reset();
    }
    public void Start() { lock (_gate) { if (!_riskLocked) _running = true; } }
    public void Pause() { lock (_gate) _running = false; }
    public void Reset() { lock (_gate) { if (_riskLocked) throw new InvalidOperationException("Bloqueio de risco exige revisao autorizada; reiniciar nao libera o bloqueio."); ResetInside(); Emit("RESET", null, null, 0m, 0m, 0m, 0m, "Cenario simulado reiniciado."); } }
    internal void ResetAfterAuthorizedReview()
    {
        lock (_gate)
        {
            if (!_riskLocked) throw new InvalidOperationException("Não há bloqueio de risco ativo para revisar.");
            if (_positions.Count != 0) throw new InvalidOperationException("Posições abertas exigem reconciliação antes de liberar o risco.");
            ResetInside();
            Emit("REVIEW_RESET", null, null, 0m, 0m, 0m, 0m, "Novo cenário autorizado após revisão humana.");
        }
    }
    private void ResetInside()
    {
        _sessionId = Guid.NewGuid(); _pendingEvents.Clear(); _traders.Clear(); _positions.Clear(); _trades.Clear(); _curve.Clear();
        foreach (var (symbol, price, vol) in Markets)
        {
            var t = new Trader { Symbol = symbol, Price = price, Previous = price, Vol = vol };
            for (var i = 0; i < 12; i++) t.History.Enqueue(price);
            _traders.Add(symbol, t);
        }
        _cash = _policy.ReferenceCapitalBrl;
        _startDayEquity = _cash; _totalContributions = _cash; _adjustedPeak = _cash;
        _contributedMonths = 0; _tick = 0; _entriesToday = 0; _id = 0;
        _running = false; _riskLocked = false;
        _riskMessage = "Somente simulação local. Testnet disponível exclusivamente para consultas públicas.";
        _curve.Add(_cash);
    }
    private decimal Value(Position p) => p.Qty * _traders[p.Symbol].Price * _policy.AssumedBrlPerUsdt;
    private decimal Equity() => _cash + _positions.Sum(Value);
    private static string SignalFor(decimal momentum, decimal vol)
    {
        if (momentum > vol * 125m) return "COMPRA";
        if (momentum < -vol * 125m) return "VENDA";
        return "AGUARDAR";
    }

    private static string ExitReason(Position position, decimal price)
    {
        if (price <= position.Stop) return "STOP";
        if (price >= position.Target) return "ALVO";
        return "TEMPO";
    }

    public void Tick()
    {
        lock (_gate)
        {
            if (_riskLocked) return;
            _tick++;
            ApplyContribution();
            BeginSimulatedDay();
            UpdatePrices();
            ExitPositions();
            EnforceRisk();
            if (!_riskLocked) EnterPosition();
            _curve.Add(Math.Round(Equity(), 2));
            if (_curve.Count > 180) _curve.RemoveAt(0);
        }
    }

    private void ApplyContribution()
    {
        var interval = checked(_policy.SimulatedTicksPerDay * _policy.SimulatedDaysPerMonth);
        if (_tick % interval != 0) return;
        var amount = _policy.MonthlyContributionBrl;
        _cash += amount;
        _totalContributions += amount;
        _adjustedPeak += amount;
        _contributedMonths++;
        Emit("CONTRIBUTION", null, null, 0m, 0m, 0m, amount, "Aporte virtual mensal");
        _riskMessage = $"Aporte virtual de R$ {amount:N2} no mes simulado {_contributedMonths}.";
    }

    private void BeginSimulatedDay()
    {
        if (_tick <= 1 || (_tick - 1) % _policy.SimulatedTicksPerDay != 0) return;
        _startDayEquity = Equity();
        _entriesToday = 0;
    }

    private void UpdatePrices()
    {
        foreach (var trader in _traders.Values)
        {
            trader.Previous = trader.Price;
            var randomMove = RandomUnit() * 2m - 1m;
            trader.Price = Math.Max(.00000001m, trader.Price * (1m + randomMove * trader.Vol));
            trader.History.Enqueue(trader.Price);
            if (trader.History.Count > 12) trader.History.Dequeue();
            var momentum = (trader.Price / trader.History.Peek() - 1m) * 100m;
            trader.Signal = SignalFor(momentum, trader.Vol);
        }
    }

    private void ExitPositions()
    {
        foreach (var position in _positions.ToArray())
        {
            var price = _traders[position.Symbol].Price;
            if (price <= position.Stop || price >= position.Target || _tick - position.OpenedTick >= 30)
                Close(position, ExitReason(position, price));
        }
    }

    private void EnforceRisk()
    {
        var equity = Equity();
        _adjustedPeak = Math.Max(_adjustedPeak, equity);
        var dailyLoss = _startDayEquity > 0 ? (_startDayEquity - equity) / _startDayEquity * 100m : 100m;
        var peakLoss = _adjustedPeak > 0 ? (_adjustedPeak - equity) / _adjustedPeak * 100m : 0m;
        if (dailyLoss < _policy.MaxDailyLossPct && peakLoss < _policy.MaxCumulativeDrawdownPct) return;
        _riskLocked = true;
        Emit("RISK_LOCK", null, null, 0m, 0m, 0m, 0m, "Limite de risco atingido; bloqueio exige revisao.");
        _running = false;
        if (peakLoss >= _policy.MaxCumulativeDrawdownPct)
            _riskMessage = $"Carlos: queda acumulada de {_policy.MaxCumulativeDrawdownPct:N2}% desde o pico ajustado por aportes. REVISAO MANUAL obrigatoria; simulacao travada.";
        else
            _riskMessage = $"Carlos: perda diaria de {_policy.MaxDailyLossPct:N2}% atingida. REVISAO MANUAL obrigatoria; simulacao travada.";
        foreach (var position in _positions.ToArray()) Close(position, "BLOQUEIO_RISCO");
        _adjustedPeak = Math.Max(_adjustedPeak, Equity());
    }

    private void EnterPosition()
    {
        if (_tick < 12 || (_policy.MaxEntriesPerDay > 0 && _entriesToday >= _policy.MaxEntriesPerDay) || _positions.Count >= _policy.MaxOpenPositions) return;
        foreach (var trader in _traders.Values.OrderBy(t => t.Symbol))
        {
            if (trader.Signal != "COMPRA" || _positions.Any(p => p.Symbol == trader.Symbol) || RandomUnit() > .15m) continue;
            if (!TryAllocation(trader, out var allocation, out var distance)) continue;
            OpenSpot(trader, allocation, distance);
            break;
        }
    }

    private bool TryAllocation(Trader trader, out decimal allocation, out decimal stopDistance)
    {
        var equity = Equity();
        stopDistance = trader.Vol * 3m;
        var budget = equity * _policy.MaxRiskPerTradePct / 100m;
        allocation = Math.Min(equity * _policy.MaxAllocationPerPositionPct / 100m, budget / stopDistance);
        allocation = Math.Min(allocation, _cash / (1m + _policy.IllustrativeFeePctPerSide / 100m));
        if (allocation < 5m) return false;
        return _positions.Sum(Value) + allocation <= equity * _policy.MaxTotalAllocationPct / 100m;
    }

    private void OpenSpot(Trader trader, decimal allocation, decimal distance)
    {
        var fee = allocation * _policy.IllustrativeFeePctPerSide / 100m;
        _cash -= allocation + fee;
        var quantity = allocation / (_policy.AssumedBrlPerUsdt * trader.Price);
        _positions.Add(new Position(++_id, trader.Symbol, trader.Price, quantity,
            trader.Price * (1m - distance), trader.Price * (1m + distance * 1.5m), allocation, fee, _tick));
        _entriesToday++;
        Emit("OPEN", _id, trader.Symbol, trader.Price, quantity, fee, allocation, "Compra Spot simulada");
    }
    private void Close(Position p, string reason)
    {
        var exitValue = Value(p);
        var exitFee = exitValue * _policy.IllustrativeFeePctPerSide / 100m;
        _cash += exitValue - exitFee;
        var pnl = exitValue - exitFee - p.EntryCostBrl - p.EntryFeeBrl;
        var trader = _traders[p.Symbol];
        trader.RealizedPnl += pnl;
        if (pnl >= 0m) trader.Wins++; else trader.Losses++;
        _trades.Insert(0, new ClosedTrade(p.Symbol, Math.Round(pnl, 2), reason, _tick));
        if (_trades.Count > 100) _trades.RemoveAt(_trades.Count - 1);
        _positions.Remove(p);
        Emit("CLOSE", p.Id, p.Symbol, _traders[p.Symbol].Price, p.Qty, exitFee, pnl, reason);
    }
    private void Emit(string kind, long? positionId, string? symbol,
        decimal price, decimal qty, decimal fee, decimal amount, string detail) =>
        _pendingEvents.Add(new SimEvent(Guid.NewGuid(), kind, positionId, symbol,
            price, qty, fee, amount, detail, _tick));

    public SimulationState ExportState()
    {
        lock (_gate)
        {
            return new SimulationState(1, _sessionId, _policy, _cash, _startDayEquity,
                _totalContributions, _adjustedPeak, _contributedMonths, _tick,
                _entriesToday, _id, _running, _riskLocked, _riskMessage,
                _traders.Values.Select(t => new SimTraderState(t.Symbol, t.Price,
                    t.Previous, t.History.ToArray(), t.Signal, t.Wins, t.Losses, t.RealizedPnl)).ToArray(),
                _positions.Select(p => new SimPositionState(p.Id, p.Symbol, p.Entry,
                    p.Qty, p.Stop, p.Target, p.EntryCostBrl, p.EntryFeeBrl, p.OpenedTick)).ToArray(),
                _trades.ToArray(), _curve.ToArray(), _pendingEvents.ToArray());
        }
    }

    public void RestoreState(SimulationState state, bool resume = false)
    {
        if (state.Version != 1 || state.SessionId == Guid.Empty || state.Policy != _policy ||
            state.Traders is null || state.Traders.Length != Markets.Length ||
            state.Traders.Any(t => !Markets.Any(m => m.Symbol == t.Symbol) ||
                t.Price <= 0m || t.Previous <= 0m || t.History is null || t.History.Length == 0) ||
            state.Positions is null || state.Positions.Any(p => p.Qty <= 0m ||
                !Markets.Any(m => m.Symbol == p.Symbol)) ||
            state.Cash < 0m || state.Tick < 0 || state.EquityHistory is null ||
            state.Trades is null || state.PendingEvents is null)
            throw new InvalidOperationException("Checkpoint RAC invalido ou politica de risco diferente. Inicializacao bloqueada.");

        lock (_gate)
        {
            _traders.Clear();
            foreach (var t in state.Traders)
            {
                var market = Markets.Single(m => m.Symbol == t.Symbol);
                var trader = new Trader
                {
                    Symbol = t.Symbol, Price = t.Price, Previous = t.Previous,
                    Vol = market.Vol, Signal = t.Signal,
                    Wins = t.Wins, Losses = t.Losses, RealizedPnl = t.RealizedPnl
                };
                foreach (var h in t.History) trader.History.Enqueue(h);
                _traders.Add(t.Symbol, trader);
            }
            _positions.Clear();
            _positions.AddRange(state.Positions.Select(p =>
                new Position(p.Id, p.Symbol, p.Entry, p.Qty, p.Stop,
                    p.Target, p.EntryCostBrl, p.EntryFeeBrl, p.OpenedTick)));
            _trades.Clear(); _trades.AddRange(state.Trades);
            _curve.Clear(); _curve.AddRange(state.EquityHistory);
            _pendingEvents.Clear(); _pendingEvents.AddRange(state.PendingEvents);
            _sessionId = state.SessionId;
            _cash = state.Cash; _startDayEquity = state.StartDayEquity;
            _totalContributions = state.TotalContributions;
            _adjustedPeak = state.AdjustedPeak;
            _contributedMonths = state.ContributedMonths;
            _tick = state.Tick; _entriesToday = state.EntriesToday; _id = state.LastPositionId;
            _riskLocked = state.RiskLocked;
            _riskMessage = state.RiskMessage;
            // Never auto-resume after an application restart.
            _running = resume && state.Running && !_riskLocked;
        }
    }

    public void ConfirmSavedEvents() { lock (_gate) _pendingEvents.Clear(); }

    public void StorageFailure()
    {
        lock (_gate)
        {
            _running = false;
            _riskLocked = true;
            _riskMessage = "Carlos: falha de persistencia. Simulacao bloqueada ate verificar o banco e reiniciar.";
        }
    }
    public RacState Snapshot()
    {
        lock (_gate)
        {
            var equity = Equity();
            var peakDrawdown = _adjustedPeak > 0m ?
                Math.Max(0m, (_adjustedPeak - equity) / _adjustedPeak * 100m) : 0m;
            var supervisors = new Supervisor[]
            {
                new("Ana", "Coordenação", _running ? "ATIVA" : "PAUSADA", "Coordena 12 traders e no máximo 1 posição"),
                new("Rebeca", "Notícias", "NÃO CONECTADA", "Sem feed de notícias; não aprova ordens reais"),
                new("Carlos", "Risco", _riskLocked ? "BLOQUEADO" : "MONITORANDO", _riskMessage)
            };
            var traders = _traders.Values.Select(t => new TraderView(t.Symbol, Math.Round(t.Price, 6),
                Math.Round((t.Price / t.Previous - 1m) * 100m, 3), t.Signal,
                _positions.Any(p => p.Symbol == t.Symbol) ? "POSIÇÃO ABERTA" : "OBSERVANDO", t.Wins, t.Losses, Math.Round(t.RealizedPnl, 2))).ToArray();
            var positions = _positions.Select(p => new PositionView(p.Id, p.Symbol, "COMPRA SPOT", p.Entry,
                _traders[p.Symbol].Price, p.Qty, Math.Round(Value(p) - p.EntryCostBrl - p.EntryFeeBrl, 2), p.Stop)).ToArray();
            return new RacState("RAC", "SIMULAÇÃO LOCAL", "PREÇOS SINTÉTICOS; NÃO SÃO COTAÇÕES BINANCE",
                false, false, _running, _tick, _policy.ReferenceCapitalBrl, Math.Round(_cash, 2),
                Math.Round(equity, 2), Math.Round(equity - _totalContributions, 2),
                Math.Round(equity - _startDayEquity, 2),
                Math.Round(Math.Max(0m, (_startDayEquity - equity) / _startDayEquity * 100m), 2),
                Math.Round(peakDrawdown, 2), Math.Round(_totalContributions, 2),
                Math.Round(equity - _totalContributions, 2), _contributedMonths,
                _riskLocked, _policy, supervisors, traders, positions, _trades.ToArray(), _curve.ToArray(),
                _entriesToday, _riskMessage, DateTimeOffset.UtcNow);
        }
    }
}
