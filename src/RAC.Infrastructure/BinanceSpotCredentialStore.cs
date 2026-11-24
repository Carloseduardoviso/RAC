using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RAC.Infrastructure;

/// <summary>
/// Windows DPAPI CurrentUser: only the Windows profile which registered the
/// credentials can decrypt them. Nothing is stored in source control or SQL.
/// </summary>
public sealed class BinanceSpotCredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RAC.Binance.Spot.ReadOnly.v1");
    private readonly string _path;

    public BinanceSpotCredentialStore(string? directory = null)
    {

        var root = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RAC");
        _path = Path.Combine(root, "binance-spot-readonly.dpapi");
    }

    public bool IsConfigured => OperatingSystem.IsWindows() && File.Exists(_path);

    public void Save(string apiKey, string apiSecret)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Length is < 20 or > 256 ||
            string.IsNullOrWhiteSpace(apiSecret) || apiSecret.Length is < 20 or > 512 ||
            apiKey.Any(char.IsWhiteSpace) || apiSecret.Any(char.IsWhiteSpace))
            throw new ArgumentException("Credenciais HMAC da Binance inválidas.");

        var plain = JsonSerializer.SerializeToUtf8Bytes(
            new StoredCredentials(apiKey, apiSecret));
        try
        {
            var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, encrypted);
                File.Move(temporary, _path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                CryptographicOperations.ZeroMemory(encrypted);
            }
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public BinanceSpotCredentials Load()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (!IsConfigured)
            throw new InvalidOperationException("Credenciais Binance Spot não configuradas.");
        var encrypted = File.ReadAllBytes(_path);
        try
        {
            var plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            try
            {
                var stored = JsonSerializer.Deserialize<StoredCredentials>(plain)
                    ?? throw new InvalidOperationException("Credenciais Binance Spot inválidas.");
                if (string.IsNullOrWhiteSpace(stored.ApiKey) ||
                    string.IsNullOrWhiteSpace(stored.ApiSecret))
                    throw new InvalidOperationException("Credenciais Binance Spot incompletas.");
                return new BinanceSpotCredentials(stored.ApiKey, stored.ApiSecret);
            }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        finally { CryptographicOperations.ZeroMemory(encrypted); }
    }

    public void Delete()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (File.Exists(_path)) File.Delete(_path);
    }

    private sealed record StoredCredentials(string ApiKey, string ApiSecret);
}

public sealed class BinanceSpotCredentials(string apiKey, string apiSecret)
{
    public string ApiKey { get; } = apiKey;
    public string ApiSecret { get; } = apiSecret;
}
