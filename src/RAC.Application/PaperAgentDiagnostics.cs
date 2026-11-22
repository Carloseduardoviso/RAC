using RAC.Domain;

namespace RAC.Application;

/// <summary>
/// Read-only diagnostics of one public Spot candle agent. A missing decision is
/// informational: HOLD signals and risk blocks do not imply a trading failure.
/// </summary>
public static class PaperAgentDiagnostics
{
    public static PaperAgentHealth Evaluate(PaperAgentCursor? agent, DateTimeOffset now)
    {
        if (agent is null)
            return new("MISSING", false, null, "Sem cursor persistido; verificar coleta de candles.");
        var age = (now - agent.LastCandleUtc).TotalSeconds;
        if (age < 0d)
            return new("CLOCK_SKEW", false, age, "Candle no futuro; verificar relogio e origem dos dados.");
        if (!string.Equals(agent.Status, "READY", StringComparison.Ordinal))
            return new("BLOCKED", false, Math.Max(0d, age),
                string.IsNullOrWhiteSpace(agent.Note) ? "Agente indisponivel; consultar coleta publica." : agent.Note);
        if (age > 240d)
            return new("STALE", false, age, "Candle atrasado acima de quatro minutos.");
        return new("READY", true, Math.Max(0d, age), "Candle atualizado; aguardando sinais e limites PAPER.");
    }
}

public sealed record PaperAgentHealth(string Status, bool Healthy, double? CandleAgeSeconds, string Reason);
