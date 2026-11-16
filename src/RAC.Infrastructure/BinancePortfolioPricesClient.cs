using System.Globalization;
using System.Net;
using System.Text.Json;

namespace RAC.Infrastructure;

/// <summary>
/// One anonymous, public GET for Binance Spot ticker prices. No keys or signed URLs.
/// </summary>
public sealed class BinancePortfolioPricesClient(HttpClient http)
{
    public async Task<PortfolioPriceBatch> GetPricesAsync(CancellationToken stoppingToken)
    {
        using var response = await http.GetAsync("api/v3/ticker/price",
            HttpCompletionOption.ResponseHeadersRead, stoppingToken);
        if (response.StatusCode is HttpStatusCode.TooManyRequests ||
            (int)response.StatusCode == 418)
            throw new InvalidOperationException("Binance restringiu temporariamente a consulta de preços.");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 2_000_000)
            throw new FormatException("Lista de cotações excede limite permitido.");

        await using var input = await response.Content.ReadAsStreamAsync(stoppingToken);
        await using var copy = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(buffer, stoppingToken)) > 0)
        {
            if (copy.Length + count > 2_000_000)
                throw new FormatException("Lista de cotações excede limite permitido.");
            await copy.WriteAsync(buffer.AsMemory(0, count), stoppingToken);
        }
        copy.Position = 0;
        using var document = await JsonDocument.ParseAsync(copy, cancellationToken: stoppingToken);
        return ParsePrices(document.RootElement, DateTimeOffset.UtcNow);
    }

    public static PortfolioPriceBatch ParsePrices(JsonElement root, DateTimeOffset observedUtc)
    {
        if (root.ValueKind != JsonValueKind.Array)
            throw new FormatException("Resposta de preços Spot inválida.");
        var prices = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var row in root.EnumerateArray())
        {
            var symbol = row.GetProperty("symbol").GetString();
            if (string.IsNullOrEmpty(symbol) || symbol.Length is > 40 or < 3 ||
                !symbol.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9'))
                continue;
            var value = decimal.Parse(row.GetProperty("price").GetString() ?? "",
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
            if (value <= 0m) continue;
            if (!prices.TryAdd(symbol, value))
                throw new FormatException("Preço Spot duplicado.");
        }
        if (prices.Count == 0) throw new FormatException("Resposta sem cotações válidas.");
        return new PortfolioPriceBatch(observedUtc, prices);
    }
}

public sealed record PortfolioPriceBatch(
    DateTimeOffset ObservedUtc, IReadOnlyDictionary<string, decimal> Prices);
