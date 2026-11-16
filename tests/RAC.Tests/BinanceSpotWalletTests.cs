using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAC.Infrastructure;

namespace RAC.Tests;

public sealed class BinanceSpotWalletTests
{
    [Fact]
    public void Parses_only_nonzero_balances_and_reports_available_usdt()
    {
        using var json = JsonDocument.Parse("""
        {"balances":[
            {"asset":"BTC","free":"0.00120000","locked":"0.00000000"},
            {"asset":"USDT","free":"35.12000000","locked":"2.00000000"},
            {"asset":"ETH","free":"0.00000000","locked":"0.00000000"}]}
        """);
        var wallet = BinanceSpotAccountClient.ParseWallet(json.RootElement, DateTimeOffset.UtcNow);
        Assert.Equal("BINANCE_SPOT_REAL", wallet.Source);
        Assert.Equal(35.12m, wallet.AvailableUsdt);
        Assert.Equal(2m, wallet.LockedUsdt);
        Assert.Equal(2, wallet.Assets.Count);
        Assert.False(wallet.RealOrdersEnabled);
    }

    [Fact]
    public void Secrets_are_encrypted_at_rest_with_current_windows_user()
    {
        if (!OperatingSystem.IsWindows()) return;
        var folder = Path.Combine(Path.GetTempPath(), "rac-wallet-unit-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new BinanceSpotCredentialStore(folder);
            var key = new string('K', 64);
            var secret = new string('S', 64);
            Assert.False(store.IsConfigured);
            store.Save(key, secret);
            var file = Directory.GetFiles(folder).Single();
            var data = File.ReadAllBytes(file);
            Assert.DoesNotContain(secret, Encoding.UTF8.GetString(data));
            Assert.Equal(key, store.Load().ApiKey);
            Assert.Equal(secret, store.Load().ApiSecret);
            store.Delete();
            Assert.False(store.IsConfigured);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public async Task Account_request_is_signed_get_and_never_trades()
    {
        if (!OperatingSystem.IsWindows()) return;
        var directory = Path.Combine(Path.GetTempPath(), "rac-wallet-api-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new BinanceSpotCredentialStore(directory);
            store.Save(new string('A', 64), new string('B', 64));
            var handler = new TestHandler(new string('B', 64));
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.binance.com/") };
            var client = new BinanceSpotAccountClient(http, store);
            var wallet = await client.GetWalletAsync(CancellationToken.None);
            Assert.Equal(50m, wallet.AvailableUsdt);
            Assert.Equal(4, handler.Paths.Count);
            Assert.Equal(2, handler.Paths.Count(p => p == "/api/v3/time"));
            Assert.Contains("/api/v3/account", handler.Paths);
            Assert.Contains("/sapi/v1/account/apiRestrictions", handler.Paths);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task Trade_enabled_keys_are_rejected_before_account_access()
    {
        if (!OperatingSystem.IsWindows()) return;
        var directory = Path.Combine(Path.GetTempPath(), "rac-wallet-unsafe-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new BinanceSpotCredentialStore(directory);
            store.Save(new string('A', 64), new string('B', 64));
            var handler = new TestHandler(new string('B', 64), unsafeKey: true);
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.binance.com/") };
            var client = new BinanceSpotAccountClient(http, store);
            await Assert.ThrowsAsync<BinanceUnsafePermissionsException>(
                () => client.GetWalletAsync(CancellationToken.None));
            Assert.DoesNotContain("/api/v3/account", handler.Paths);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true); }
    }

    private sealed class TestHandler(string secret, bool unsafeKey = false) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.Method != HttpMethod.Get ||
                request.RequestUri?.Host != "api.binance.com")
                throw new InvalidOperationException("A carteira deve usar apenas GET no host oficial.");
            var path = request.RequestUri.AbsolutePath;
            Paths.Add(path);
            var content = path switch
            {
                "/api/v3/time" => """{"serverTime":1771000000000}""",
                "/sapi/v1/account/apiRestrictions" => PermissionsJson(unsafeKey),
                "/api/v3/account" => """
                    {"balances":[{"asset":"USDT","free":"50.00000000","locked":"0.00000000"}]}
                    """,
                _ => throw new InvalidOperationException("Endpoint não autorizado.")
            };
            if (path != "/api/v3/time")
            {
                Assert.Equal(new string('A', 64), request.Headers.GetValues("X-MBX-APIKEY").Single());
                var query = request.RequestUri.Query.TrimStart('?');
                var parts = query.Split("&signature=", 2);
                Assert.Equal(2, parts.Length);
                Assert.Contains("recvWindow=5000", parts[0]);
                Assert.Contains("timestamp=", parts[0]);
                var expected = Convert.ToHexString(HMACSHA256.HashData(
                    Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(parts[0])))
                    .ToLowerInvariant();
                Assert.Equal(expected, parts[1]);
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            });
        }

        private static string PermissionsJson(bool unsafeKey) => $$"""
            {
              "enableReading":true,
              "enableWithdrawals":false,
              "enableSpotAndMarginTrading":{{unsafeKey.ToString().ToLowerInvariant()}},
              "enableMargin":false,
              "enableFutures":false,
              "enablePortfolioMarginTrading":false,
              "enableInternalTransfer":false,
              "permitsUniversalTransfer":false
            }
            """;
    }
}
