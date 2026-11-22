using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAC.Domain;

namespace RAC.Infrastructure;

/// <summary>
/// Explicit, read-only Binance activity import. The wallet reader checks that
/// the API key has no trading, withdrawal or transfer permissions before use.
/// </summary>
public sealed class BinanceSpotActivityClient(
    HttpClient http, BinanceSpotCredentialStore store, BinanceSpotAccountClient wallet)
{
    public static readonly string[] AllowedSymbols =
    [
        "BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT",
        "XRPUSDT", "ADAUSDT", "DOGEUSDT", "LINKUSDT",
        "AVAXUSDT", "LTCUSDT", "DOTUSDT", "TRXUSDT"
    ];

    public async Task<SpotActivityBatch> FetchAsync(string symbol, bool includeTransfers,
        CancellationToken ct)
    {
        if (!AllowedSymbols.Contains(symbol, StringComparer.Ordinal))
            throw new ArgumentException("Par de negociação não autorizado.");
        // The account client fails closed on unexpected/dangerous permissions.
        await wallet.GetWalletAsync(ct);
        var stored = store.Load();
        var entries = new List<SpotActivity>();
        var warnings = new List<string>();
        using (var trades = await SignedGetAsync("api/v3/myTrades",
            "symbol=" + symbol + "&limit=100", stored, ct))
        {
            if (trades.RootElement.ValueKind != JsonValueKind.Array)
                throw new FormatException("Resposta de negociações inválida.");
            foreach (var item in trades.RootElement.EnumerateArray())
            {
                var id = Ref(item.GetProperty("id"));
                var quantity = PositiveDecimal(item.GetProperty("qty"));
                var price = PositiveDecimal(item.GetProperty("price"));
                var quote = PositiveDecimal(item.GetProperty("quoteQty"));
                var fee = PositiveDecimal(item.GetProperty("commission"));
                var commissionAsset = Asset(item.GetProperty("commissionAsset").GetString());
                entries.Add(new SpotActivity
                {
                    Kind = "TRADE", ReferenceId = id, Symbol = symbol,
                    Asset = symbol[..^4], Side = item.GetProperty("isBuyer").GetBoolean() ? "BUY" : "SELL",
                    Status = "FILLED", Quantity = quantity, Price = price,
                    QuoteQuantity = quote, Fee = fee, FeeAsset = commissionAsset,
                    OccurredUtc = Millis(item.GetProperty("time"))
                });
            }
            if (trades.RootElement.GetArrayLength() == 100)
                warnings.Add("Negociações: limite de 100 registros atingido para " + symbol + "; histórico incompleto.");
        }

        if (includeTransfers)
        {
            var now = DateTimeOffset.UtcNow;
            var since = now.AddDays(-30).ToUnixTimeMilliseconds();
            var until = now.ToUnixTimeMilliseconds();
            var query = $"startTime={since}&endTime={until}&limit=100";

            // Each API family has its own errors and permissions; never report
            // a fully reconciled history if either family fails.
            try
            {
                using var deposits = await SignedGetAsync(
                    "sapi/v1/capital/deposit/hisrec", query, stored, ct);
                if (deposits.RootElement.ValueKind != JsonValueKind.Array)
                    throw new FormatException("Histórico de depósitos inválido.");
                foreach (var item in deposits.RootElement.EnumerateArray())
                {
                    var coin = Asset(item.GetProperty("coin").GetString());
                    entries.Add(new SpotActivity
                    {
                        Kind = "DEPOSIT", ReferenceId = Ref(item.GetProperty("id")),
                        Symbol = "", Asset = coin, Side = "",
                        Status = "BINANCE_" + Ref(item.GetProperty("status")),
                        Quantity = PositiveDecimal(item.GetProperty("amount")),
                        Price = 0m, QuoteQuantity = 0m, Fee = 0m, FeeAsset = coin,
                        OccurredUtc = Millis(item.GetProperty("insertTime"))
                    });
                }
                if (deposits.RootElement.GetArrayLength() == 100)
                    warnings.Add("Depósitos: limite de 100 atingido nos últimos 30 dias.");
            }
            catch (Exception ex) when (ex is BinanceWalletRequestException or FormatException or
                KeyNotFoundException or InvalidOperationException or JsonException)
            { warnings.Add("Depósitos não puderam ser consultados; histórico parcial."); }

            try
            {
                using var withdrawals = await SignedGetAsync(
                    "sapi/v1/capital/withdraw/history", query, stored, ct);
                if (withdrawals.RootElement.ValueKind != JsonValueKind.Array)
                    throw new FormatException("Histórico de retiradas inválido.");
                foreach (var item in withdrawals.RootElement.EnumerateArray())
                {
                    var coin = Asset(item.GetProperty("coin").GetString());
                    var occurred = item.TryGetProperty("completeTime", out var completed) &&
                        completed.ValueKind == JsonValueKind.String &&
                        DateTimeOffset.TryParse(completed.GetString(), CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeUniversal, out var parsed)
                            ? parsed : DateTimeOffset.Parse(
                                item.GetProperty("applyTime").GetString()!,
                                CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                    entries.Add(new SpotActivity
                    {
                        Kind = "WITHDRAWAL", ReferenceId = Ref(item.GetProperty("id")),
                        Symbol = "", Asset = coin, Side = "",
                        Status = "BINANCE_" + Ref(item.GetProperty("status")),
                        Quantity = PositiveDecimal(item.GetProperty("amount")),
                        Price = 0m, QuoteQuantity = 0m,
                        Fee = item.TryGetProperty("transactionFee", out var transactionFee)
                            ? PositiveDecimal(transactionFee) : 0m,
                        FeeAsset = coin, OccurredUtc = occurred
                    });
                }
                if (withdrawals.RootElement.GetArrayLength() == 100)
                    warnings.Add("Retiradas: limite de 100 atingido nos últimos 30 dias.");
            }
            catch (Exception ex) when (ex is BinanceWalletRequestException or FormatException or
                KeyNotFoundException or InvalidOperationException or JsonException)
            { warnings.Add("Retiradas não puderam ser consultadas; histórico parcial."); }
        }
        else
        {
            warnings.Add("Depósitos e retiradas não consultados nesta execução.");
        }

        warnings.Add("Negociações: somente os últimos 100 registros do par; outros pares e históricos antigos não foram conciliados.");
        return new SpotActivityBatch(entries, warnings,
            DateTimeOffset.UtcNow, Source: "BINANCE_SPOT_READ_ONLY");
    }

    private async Task<JsonDocument> SignedGetAsync(string path, string parameters,
        BinanceSpotCredentials credentials, CancellationToken ct)
    {
        using var timeResponse = await http.GetAsync("api/v3/time", ct);
        if (!timeResponse.IsSuccessStatusCode)
            throw new BinanceWalletRequestException((int)timeResponse.StatusCode);
        using var timeJson = JsonDocument.Parse(await timeResponse.Content.ReadAsStringAsync(ct));
        var timestamp = timeJson.RootElement.GetProperty("serverTime").GetInt64();
        var query = parameters + $"&recvWindow=5000&timestamp={timestamp}";
        var secretBytes = Encoding.UTF8.GetBytes(credentials.ApiSecret);
        try
        {
            var signature = Convert.ToHexString(HMACSHA256.HashData(secretBytes,
                Encoding.UTF8.GetBytes(query))).ToLowerInvariant();
            using var request = new HttpRequestMessage(
                HttpMethod.Get, path + "?" + query + "&signature=" + signature);
            request.Headers.TryAddWithoutValidation("X-MBX-APIKEY", credentials.ApiKey);
            using var response = await http.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                throw new BinanceWalletRequestException((int)response.StatusCode);
            // Bound the response size even when Content-Length is missing.
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var buffer = new MemoryStream();
            var block = new byte[8192];
            int count;
            while ((count = await source.ReadAsync(block, ct)) != 0)
            {
                if (buffer.Length + count > 512_000)
                    throw new FormatException("Resposta Binance excede o tamanho máximo.");
                await buffer.WriteAsync(block.AsMemory(0, count), ct);
            }
            buffer.Position = 0;
            return await JsonDocument.ParseAsync(buffer, cancellationToken: ct);
        }
        finally { CryptographicOperations.ZeroMemory(secretBytes); }
    }

    private static string Ref(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString() ?? "",
        JsonValueKind.Number => el.GetRawText(),
        _ => throw new FormatException("Identificador Binance inválido.")
    };

    private static string Asset(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 20 &&
        value.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9')
            ? value : throw new FormatException("Ativo Binance inválido.");

    private static decimal PositiveDecimal(JsonElement element)
    {
        var raw = Ref(element);
        if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var value) || value < 0m)
            throw new FormatException("Quantia Binance inválida.");
        return value;
    }

    private static DateTimeOffset Millis(JsonElement element) =>
        DateTimeOffset.FromUnixTimeMilliseconds(element.GetInt64());
}

public sealed record SpotActivityBatch(
    IReadOnlyList<SpotActivity> Entries, IReadOnlyList<string> Warnings,
    DateTimeOffset ObservedUtc, string Source);
