using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RAC.Infrastructure;

/// <summary>
/// Isolated test-funds-only credentials. A completely different DPAPI file
/// and entropy from the live READ ONLY Binance key.
/// </summary>
public sealed class BinanceTestnetCredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("RAC.Binance.Spot.Testnet.Only.v1");
    private readonly string _path;

    public BinanceTestnetCredentialStore(string? directory = null)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows DPAPI required.");
        var folder = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RAC");
        _path = Path.Combine(folder, "binance-spot-testnet-only.dpapi");
    }

    public bool IsConfigured => File.Exists(_path);

    public void Save(string key, string secret)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret) ||
            key.Length is < 20 or > 256 || secret.Length is < 20 or > 512 ||
            key.Any(char.IsWhiteSpace) || secret.Any(char.IsWhiteSpace))
            throw new ArgumentException("Credenciais HMAC de teste inválidas.");
        var plain = JsonSerializer.SerializeToUtf8Bytes(new Credentials(key,secret));
        try
        {
            var protectedBytes = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                var temp = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try { File.WriteAllBytes(temp,protectedBytes); File.Move(temp,_path,true); }
                finally { if (File.Exists(temp)) File.Delete(temp); }
            }
            finally { CryptographicOperations.ZeroMemory(protectedBytes); }
        }
        finally { CryptographicOperations.ZeroMemory(plain); }
    }

    public (string ApiKey,string ApiSecret) Load()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        if (!IsConfigured) throw new InvalidOperationException("Configure credenciais exclusivas da Testnet.");
        var encrypted = File.ReadAllBytes(_path);
        try
        {
            var plain = ProtectedData.Unprotect(encrypted,Entropy,DataProtectionScope.CurrentUser);
            try
            {
                var value = JsonSerializer.Deserialize<Credentials>(plain)
                    ?? throw new InvalidOperationException("Credenciais de Testnet inválidas.");
                return (value.ApiKey,value.ApiSecret);
            }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        finally { CryptographicOperations.ZeroMemory(encrypted); }
    }

    private sealed record Credentials(string ApiKey,string ApiSecret);
}
