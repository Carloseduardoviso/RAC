namespace RAC.Application;

/// <summary>Pure, read-only operational checks; does not control any worker.</summary>
public static class RacSafetyDiagnostics
{
    public sealed record Snapshot(
        bool PaperPresent,
        bool PaperRunning,
        bool PaperRiskLocked,
        DateTimeOffset? LastPaperCycleUtc,
        DateTimeOffset? LastMonitorSampleUtc,
        bool TestnetPresent,
        bool TestnetEnabled,
        bool TestnetEmergencyStopped,
        int TestnetOrderCount,
        int OpenPaperAlerts,
        int PendingPolicyReviews,
        DateTimeOffset ObservedUtc);

    public sealed record Incident(string Code, string Severity, string Message);
    public sealed record Report(
        string Status, string Mode, DateTimeOffset ObservedUtc,
        bool PaperRunning, bool PaperRiskLocked, bool TestnetEnabled,
        bool RealOrdersEnabled, int TestnetOrders, int PendingReviews,
        IReadOnlyList<Incident> Incidents);

    public static Report Evaluate(Snapshot input)
    {
        var incidents = new List<Incident>();
        if (!input.PaperPresent)
            incidents.Add(new("PAPER_ACCOUNT_MISSING", "CRITICAL", "Conta PAPER não encontrada."));
        else
        {
            if (input.PaperRiskLocked)
                incidents.Add(new("PAPER_RISK_LOCK", "CRITICAL", "Bloqueio de risco PAPER ativo."));
            if (input.PaperRunning && (input.LastPaperCycleUtc is null ||
                input.LastPaperCycleUtc > input.ObservedUtc ||
                input.ObservedUtc - input.LastPaperCycleUtc > TimeSpan.FromMinutes(5)))
                incidents.Add(new("PAPER_CYCLE_STALE", "WARNING", "Ciclo PAPER ausente ou atrasado."));
        }
        if (!input.TestnetPresent)
            incidents.Add(new("TESTNET_STATE_MISSING", "WARNING", "Estado Testnet não encontrado."));
        else if (input.TestnetEnabled)
            incidents.Add(new("TESTNET_UNEXPECTEDLY_ENABLED", "CRITICAL",
                "Testnet habilitada; verificar separadamente sem executar ordens."));
        if (input.TestnetOrderCount > 0)
            incidents.Add(new("TESTNET_ORDER_HISTORY", "INFO",
                "Existem registros de ordens Testnet históricas."));
        if (input.OpenPaperAlerts > 0)
            incidents.Add(new("PAPER_MONITOR_ALERTS", "WARNING",
                "Há incidentes PAPER persistidos em aberto."));
        if (input.PendingPolicyReviews > 0)
            incidents.Add(new("POLICY_REVIEWS_PENDING", "INFO",
                "Há propostas de risco aguardando revisão documental."));
        if (input.LastMonitorSampleUtc is null ||
            input.LastMonitorSampleUtc > input.ObservedUtc ||
            input.ObservedUtc - input.LastMonitorSampleUtc > TimeSpan.FromMinutes(5))
            incidents.Add(new("MONITOR_SAMPLE_STALE", "WARNING",
                "Amostra recente de monitoramento PAPER indisponível."));
        var status = incidents.Any(x => x.Severity == "CRITICAL") ? "CRITICAL" :
            incidents.Any(x => x.Severity == "WARNING") ? "ATTENTION" : "OK";
        return new(status, "READ_ONLY_SAFETY_DIAGNOSTICS", input.ObservedUtc,
            input.PaperRunning, input.PaperRiskLocked, input.TestnetEnabled,
            false, input.TestnetOrderCount, input.PendingPolicyReviews, incidents);
    }
}
