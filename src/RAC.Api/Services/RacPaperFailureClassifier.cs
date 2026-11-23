using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace RAC.Api.Services;

/// <summary>
/// Produces auditable, bounded risk categories without persisting raw exception
/// text, SQL connection details, HTTP tokens or other potentially private data.
/// Classification does not grant permission to retry or resume PAPER.
/// </summary>
public static class RacPaperFailureClassifier
{
    public static string Classify(Exception failure)
    {
        for (Exception? current = failure; current is not null; current = current.InnerException)
        {
            if (current is SqlException or DbUpdateException)
                return "SQL_PERSISTENCE";
            if (current is HttpRequestException or System.IO.IOException)
                return "PUBLIC_NETWORK";
            if (current is TimeoutException or TaskCanceledException)
                return "TIMEOUT";
        }

        if (failure is InvalidOperationException)
        {
            var message = failure.Message;
            if (message.StartsWith("Sem cotação pública", StringComparison.Ordinal) ||
                message.StartsWith("Sem preço público", StringComparison.Ordinal) ||
                message.StartsWith("Oscilação atípica", StringComparison.Ordinal) ||
                message.StartsWith("Sem candles", StringComparison.Ordinal) ||
                message.StartsWith("Sinal de posição", StringComparison.Ordinal))
                return "MARKET_DATA_VALIDATION";
            if (message.StartsWith("Muitas contribuições virtuais", StringComparison.Ordinal))
                return "PAPER_STATE_VALIDATION";
        }
        return "UNEXPECTED";
    }

    public static string SafeReason(Exception failure) =>
        $"Carlos: falha PAPER ({Classify(failure)}). Revisão obrigatória.";
}
