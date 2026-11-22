using RAC.Domain;

namespace RAC.Application;

/// <summary>Read-only status derived from durable PAPER timestamps; does not change execution.</summary>
public static class PaperCycleDiagnostics
{
    public static PaperCycleHealth Evaluate(PaperAccount? account, DateTimeOffset now)
    {
        if (account is null) return new("UNINITIALIZED", null, "Carteira PAPER ainda nao inicializada.");
        var age = account.LastCycleUtc is null ? (double?)null :
            Math.Max(0d, (now - account.LastCycleUtc.Value).TotalSeconds);
        if (account.RiskLocked)
            return new("RISK_LOCKED", age, "Bloqueio financeiro: revisao manual obrigatoria.");
        if (!account.Running)
            return new("PAUSED", age, "Carteira PAPER pausada.");
        // A manual START is not a completed cycle, but provides a startup grace period.
        var activity = account.LastCycleUtc is null || account.UpdatedUtc > account.LastCycleUtc
            ? account.UpdatedUtc : account.LastCycleUtc.Value;
        if (activity > now.AddMinutes(1))
            return new("CLOCK_SKEW", age, "Timestamp PAPER no futuro: verificar relogio.");
        if (now - activity > TimeSpan.FromMinutes(4))
            return new("STALLED", age, "Motor PAPER sem ciclo recente; verificar worker e logs.");
        if (account.LastCycleUtc is null || account.UpdatedUtc > account.LastCycleUtc)
            return new("STARTING", age, "PAPER iniciado; aguardando primeiro ciclo do worker.");
        return new("RUNNING", age, "Ultimo ciclo PAPER dentro da janela de quatro minutos.");
    }
}

public sealed record PaperCycleHealth(string Status, double? LastCycleAgeSeconds, string Reason);
