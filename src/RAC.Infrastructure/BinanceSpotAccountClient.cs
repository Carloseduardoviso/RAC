using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RAC.Infrastructure;

/// <summary>
/// Binance SPOT account reader. The only HTTP methods used here are GET
/// (server time, API key restrictions, and account information). No order methods.
/// </summary>
public sealed class BinanceSpotAccountClient(
    HttpClient client, BinanceSpotCredentialStore credentials)
{
    public async Task<BinanceSpotWallet> GetWalletAsync(CancellationToken ct)
    {
        var stored = credentials.Load(); // Decrypted only during this request.
        using (var permissions = await SignedGetAsync(
            "sapi/v1/account/apiRestrictions", stored, ct))
        {
            var root = permissions.RootElement;
            if (!FlagMatches(root, "enableReading", true) ||
                !FlagMatches(root, "enableWithdrawals", false) ||
                !FlagMatches(root, "enableSpotAndMarginTrading", false) ||
                !FlagMatches(root, "enableMargin", false) ||
                !FlagMatches(root, "enableFutures", false) ||
                !FlagMatches(root, "enablePortfolioMarginTrading", false) ||
                !FlagMatches(root, "enableInternalTransfer", false) ||
                !FlagMatches(root, "permitsUniversalTransfer", false))
                throw new BinanceUnsafePermissionsException();
        }

        using var account = await SignedGetAsync("api/v3/account", stored, ct,
            "omitZeroBalances=true");
        return ParseWallet(account.RootElement, DateTimeOffset.UtcNow);
    }

    private async Task<JsonDocument> SignedGetAsync(string path,
        BinanceSpotCredentials stored, CancellationToken ct, string? extraQuery = null)
    {
        // The signing time comes from Binance to avoid clock drift on the computer.
        var timestamp = await ServerTimeAsync(ct);
        var parameters = (extraQuery is null ? "" : extraQuery + "&") +
            $"recvWindow=5000&timestamp={timestamp}";
        var secret = Encoding.UTF8.GetBytes(stored.ApiSecret);
        try
        {
            var bytes = Encoding.UTF8.GetBytes(parameters);
            var signature = Convert.ToHexString(HMACSHA256.HashData(secret, bytes))
                .ToLowerInvariant();
            var query = parameters + "&signature=" + signature;
            using var request = new HttpRequestMessage(HttpMethod.Get, path + "?" + query);
            request.Headers.TryAddWithoutValidation("X-MBX-APIKEY", stored.ApiKey);
            return await ReadJsonAsync(request, ct);
        }
        finally { CryptographicOperations.ZeroMemory(secret); }
    }

    private async Task<long> ServerTimeAsync(CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v3/time");
        using var document = await ReadJsonAsync(request, ct);
        var millis = document.RootElement.GetProperty("serverTime").GetInt64();
        if (millis <= 0) throw new FormatException("Relógio da Binance inválido.");
        return millis;
    }

    private async Task<JsonDocument> ReadJsonAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        using var response = await client.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Never include the signed URI, headers or payload in exceptions/logs.
            throw new BinanceWalletRequestException((int)response.StatusCode);
        }
        if (response.Content.Headers.ContentLength > 512_000)
            throw new FormatException("Resposta Binance excede o tamanho esperado.");
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        // Hard cap for responses without a Content-Length header.
        await using var memory = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) != 0)
        {
            if (memory.Length + read > 512_000)
                throw new FormatException("Resposta Binance excede o tamanho esperado.");
            await memory.WriteAsync(buffer.AsMemory(0, read), ct);
        }
        memory.Position = 0;
        return await JsonDocument.ParseAsync(memory, cancellationToken: ct);
    }

    private static bool FlagMatches(JsonElement root, string name, bool expected) =>
        root.TryGetProperty(name, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        value.GetBoolean() == expected;

    public static BinanceSpotWallet ParseWallet(
        JsonElement root, DateTimeOffset observedUtc)
    {
        var assets = new List<BinanceSpotAsset>();
        foreach (var asset in root.GetProperty("balances").EnumerateArray())
        {
            var name = asset.GetProperty("asset").GetString();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 20 ||
                !name.All(char.IsLetterOrDigit))
                throw new FormatException("Ativo de carteira inválido.");
            var free = ParseDecimal(asset.GetProperty("free"));
            var locked = ParseDecimal(asset.GetProperty("locked"));
            if (free < 0 || locked < 0)
                throw new FormatException("Saldo Binance inválido.");
            if (free != 0 || locked != 0)
                assets.Add(new BinanceSpotAsset(name, free, locked));
        }
        assets.Sort((a, b) => StringComparer.Ordinal.Compare(a.Asset, b.Asset));
        var usdt = assets.FirstOrDefault(x => x.Asset == "USDT");
        return new BinanceSpotWallet("BINANCE_SPOT_REAL", observedUtc,
            usdt?.Free ?? 0m, usdt?.Locked ?? 0m,
            assets, RealOrdersEnabled: false);
    }

    private static decimal ParseDecimal(JsonElement value) =>
        decimal.Parse(value.GetString() ?? throw new FormatException("Saldo ausente."),
            NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
}

public sealed record BinanceSpotAsset(string Asset, decimal Free, decimal Locked);

public sealed record BinanceSpotWallet(string Source, DateTimeOffset ObservedUtc,
    decimal AvailableUsdt, decimal LockedUsdt,
    IReadOnlyList<BinanceSpotAsset> Assets, bool RealOrdersEnabled);

public sealed class BinanceUnsafePermissionsException()
    : Exception("A chave Binance permite transações ou transferências; o RAC exige leitura exclusiva.");

public sealed class BinanceWalletRequestException(int status)
    : Exception("Falha ao consultar conta Binance Spot.")
{
    public int HttpStatus { get; } = status;
}
