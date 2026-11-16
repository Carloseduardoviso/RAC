using System.Globalization;
using System.Net;
using System.Text.Json;
using RAC.Domain;

namespace RAC.Infrastructure;

/// <summary>
/// Public Binance Spot market data only. This client has no credentials and exposes
/// no order or account methods.
/// </summary>
public sealed class BinancePublicMarketClient(HttpClient client)
{
    public async Task<IReadOnlyList<MarketPricePoint>> GetPricesAsync(CancellationToken ct)
    {
        var symbols = Uri.EscapeDataString(JsonSerializer.Serialize(RacMarketSymbols.Supported));
        using var document = await FetchAsync($"api/v3/ticker/price?symbols={symbols}", ct);
        return ParsePrices(document.RootElement);
    }

    public async Task<IReadOnlyList<MarketCandlePoint>> GetClosedOneMinuteCandlesAsync(
        string symbol, int count, CancellationToken ct)
    {
        if (!RacMarketSymbols.IsSupported(symbol)) throw new ArgumentOutOfRangeException(nameof(symbol));
        if (count is < 2 or > 120) throw new ArgumentOutOfRangeException(nameof(count));
        using var document = await FetchAsync(
            $"api/v3/klines?symbol={symbol}&interval=1m&limit={count}", ct);
        return ParseClosedCandles(symbol, document.RootElement, DateTimeOffset.UtcNow);
    }

    private async Task<JsonDocument> FetchAsync(string path, CancellationToken ct)
    {
        using var response = await client.GetAsync(path, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests ||
            (int)response.StatusCode == 418)
        {
            var seconds = response.Headers.RetryAfter?.Delta?.TotalSeconds
                ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow)?.TotalSeconds
                ?? 300;
            var delay = TimeSpan.FromSeconds(Math.Clamp(seconds, 60, 900));
            throw new BinanceRateLimitException(delay);
        }
        response.EnsureSuccessStatusCode();
        await using var content = await response.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(content, cancellationToken: ct);
    }

    public static IReadOnlyList<MarketPricePoint> ParsePrices(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array)
            throw new FormatException("Ticker da Binance não retornou uma lista.");
        var prices = new List<MarketPricePoint>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in root.EnumerateArray())
        {
            var symbol = entry.GetProperty("symbol").GetString();
            if (!RacMarketSymbols.IsSupported(symbol)) continue;
            if (!seen.Add(symbol!)) throw new FormatException("Ticker duplicado.");
            var price = DecimalValue(entry.GetProperty("price"));
            if (price <= 0) throw new FormatException("Cotação inválida.");
            prices.Add(new MarketPricePoint(symbol!, price));
        }
        if (prices.Count == 0) throw new FormatException("Nenhum dos 12 pares foi retornado.");
        return prices;
    }

    public static IReadOnlyList<MarketCandlePoint> ParseClosedCandles(
        string symbol, JsonElement root, DateTimeOffset now)
    {
        if (!RacMarketSymbols.IsSupported(symbol)) throw new ArgumentOutOfRangeException(nameof(symbol));
        if (root.ValueKind != JsonValueKind.Array) throw new FormatException("Candles inválidos.");
        var candles = new List<MarketCandlePoint>();
        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Array || item.GetArrayLength() < 9)
                throw new FormatException("Candle da Binance incompleto.");
            var openTime = DateTimeOffset.FromUnixTimeMilliseconds(item[0].GetInt64());
            var closeTime = DateTimeOffset.FromUnixTimeMilliseconds(item[6].GetInt64());
            if (openTime >= now || closeTime <= openTime ||
                closeTime >= now.AddSeconds(-1)) continue; // Do not persist a candle still forming.
            var open = DecimalValue(item[1]);
            var high = DecimalValue(item[2]);
            var low = DecimalValue(item[3]);
            var close = DecimalValue(item[4]);
            var volume = DecimalValue(item[5]);
            var quoteVolume = DecimalValue(item[7]);
            var trades = item[8].GetInt64();
            if (low <= 0 || high < Math.Max(open, close) || low > Math.Min(open, close) ||
                volume < 0 || quoteVolume < 0 || trades < 0)
                throw new FormatException("OHLCV inconsistente.");
            candles.Add(new MarketCandlePoint(symbol, openTime, closeTime,
                open, high, low, close, volume, quoteVolume, trades));
        }
        return candles;
    }

    private static decimal DecimalValue(JsonElement property) =>
        decimal.Parse(property.GetString() ?? throw new FormatException("Número ausente."),
            NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
}

public sealed class BinanceRateLimitException(TimeSpan retryAfter)
    : Exception("Binance limitou as consultas públicas.")
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}
