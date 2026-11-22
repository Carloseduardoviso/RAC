using RAC.Domain;

namespace RAC.Application;

/// <summary>Deterministic health policy. A paused account is not a failure.</summary>
public static class PaperMonitoringPolicy
{
    public static IReadOnlyDictionary<string, (string Severity, string Message)> Evaluate(
        PaperAccount account, IReadOnlyList<PaperAgentCursor> agents,
        DateTimeOffset? lastPublicCandle, DateTimeOffset now)
    {
        var incidents = new Dictionary<string, (string, string)>(StringComparer.Ordinal);
        if (account.RiskLocked)
            incidents["RISK_LOCKED"] = ("CRITICAL", "Carlos bloqueou a carteira fictícia. Revisão manual obrigatória.");
        if (account.Running)
        {
            // The most recent explicit START also counts as activity. Give
            // the worker a bounded grace period for its first scheduled cycle.
            var recentActivity = account.LastCycleUtc is null ||
                account.UpdatedUtc > account.LastCycleUtc
                ? account.UpdatedUtc : account.LastCycleUtc.Value;
            if (now - recentActivity > TimeSpan.FromMinutes(4))
                incidents["PAPER_CYCLE_STALE"] = ("CRITICAL", "Motor de operações fictícias sem ciclo recente.");
            if (lastPublicCandle is null || now - lastPublicCandle > TimeSpan.FromMinutes(4))
                incidents["MARKET_STALE"] = ("CRITICAL", "Candles públicos desatualizados.");
            if (agents.Count != RacMarketSymbols.Supported.Length ||
                agents.Count(x => x.Status == "READY") < RacMarketSymbols.Supported.Length)
                incidents["AGENTS_NOT_READY"] = ("WARNING", "Um ou mais agentes não possuem sinal de candle válido.");
        }
        if (account.CashBrl < 0m || account.LastEquityBrl < 0m)
            incidents["INVALID_PAPER_BALANCE"] = ("CRITICAL", "Saldo virtual negativo: inconsistência contábil.");
        if (account.LastEquityBrl > 0m && account.PeakEquityBrl > 0m &&
            (account.PeakEquityBrl - account.LastEquityBrl) /
                account.PeakEquityBrl * 100m >= PaperRisk.MaxDrawdownPct)
            incidents["DRAWDOWN_LIMIT"] = ("CRITICAL", "Queda acumulada virtual atingiu o limite de 10%.");
        if (account.DailyStartEquityBrl > 0m &&
            (account.DailyStartEquityBrl - account.LastEquityBrl) /
                account.DailyStartEquityBrl * 100m >= PaperRisk.DailyLossLimitPct)
            incidents["DAILY_LOSS_LIMIT"] = ("CRITICAL", "Perda diária virtual atingiu o limite de 1%.");
        return incidents;
    }

    public static decimal DrawdownPercent(decimal equity, decimal peak) =>
        peak <= 0m ? 0m : Math.Max(0m, (peak - equity) / peak * 100m);
}
