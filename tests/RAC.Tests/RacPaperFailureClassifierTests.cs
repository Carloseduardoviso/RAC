using Microsoft.EntityFrameworkCore;
using RAC.Api.Services;

namespace RAC.Tests;

public sealed class RacPaperFailureClassifierTests
{
    [Theory]
    [InlineData("Sem cotação pública atual USDT/BRL.", "MARKET_DATA_VALIDATION")]
    [InlineData("Sem preço público para posição aberta.", "MARKET_DATA_VALIDATION")]
    [InlineData("Sem candles para posição aberta.", "MARKET_DATA_VALIDATION")]
    [InlineData("Oscilação atípica de preço; intervenção necessária.", "MARKET_DATA_VALIDATION")]
    [InlineData("Sinal de posição aberta inválido ou obsoleto.", "MARKET_DATA_VALIDATION")]
    [InlineData("Muitas contribuições virtuais pendentes.", "PAPER_STATE_VALIDATION")]
    [InlineData("Other unexpected failure: secret-password", "UNEXPECTED")]
    public void Known_and_unknown_validation_fail_closed(string message, string category)
    {
        var failure = new InvalidOperationException(message);
        Assert.Equal(category, RacPaperFailureClassifier.Classify(failure));
        var reason = RacPaperFailureClassifier.SafeReason(failure);
        Assert.Contains(category, reason);
        Assert.Contains("Revisão obrigatória", reason);
        Assert.DoesNotContain("secret-password", reason);
    }

    [Fact]
    public void Nested_sql_failures_are_classified_without_leaking_connection_information()
    {
        var failure = new InvalidOperationException("connection details are private",
            new DbUpdateException("Server=private-db;Password=private-password"));
        Assert.Equal("SQL_PERSISTENCE", RacPaperFailureClassifier.Classify(failure));
        var reason = RacPaperFailureClassifier.SafeReason(failure);
        Assert.DoesNotContain("private-db", reason);
        Assert.DoesNotContain("private-password", reason);
    }

    [Fact]
    public void Public_network_failures_are_named_without_persisting_url_or_credentials()
    {
        var failure = new HttpRequestException("https://example.invalid?token=private-token");
        Assert.Equal("PUBLIC_NETWORK", RacPaperFailureClassifier.Classify(failure));
        Assert.DoesNotContain("private-token", RacPaperFailureClassifier.SafeReason(failure));
    }

    [Fact]
    public void Timeout_category_never_implies_automatic_resume()
    {
        var failure = new TimeoutException("timeout with sensitive payload");
        Assert.Equal("TIMEOUT", RacPaperFailureClassifier.Classify(failure));
        Assert.Contains("Revisão obrigatória", RacPaperFailureClassifier.SafeReason(failure));
        Assert.DoesNotContain("sensitive payload", RacPaperFailureClassifier.SafeReason(failure));
    }
}
