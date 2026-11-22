using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

public sealed class SpotActivityImportTests
{
    [Fact]
    public async Task Parses_read_only_spot_history_and_does_not_place_orders()
    {
        if (!OperatingSystem.IsWindows()) return; // Windows DPAPI CurrentUser
        var folder = Path.Combine(Path.GetTempPath(), "RAC-ActivityTests-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new BinanceSpotCredentialStore(folder);
            store.Save(new string('K', 64), new string('S', 64));
            using var http = new HttpClient(new StubBinanceHandler())
                { BaseAddress = new Uri("https://api.binance.com/") };
            var reader = new BinanceSpotAccountClient(http, store);
            var history = new BinanceSpotActivityClient(http, store, reader);
            var result = await history.FetchAsync("BTCUSDT", true, CancellationToken.None);
            Assert.Equal("BINANCE_SPOT_READ_ONLY", result.Source);
            Assert.Equal(3, result.Entries.Count);
            var trade = Assert.Single(result.Entries.Where(x => x.Kind == "TRADE"));
            Assert.Equal("BTCUSDT", trade.Symbol);
            Assert.Equal("BUY", trade.Side);
            Assert.Equal(0.001m, trade.Quantity);
            Assert.Equal(90m, trade.QuoteQuantity);
            Assert.Equal(0.000001m, trade.Fee);
            Assert.Equal("BTC", trade.FeeAsset);
            Assert.Contains(result.Entries, x => x.Kind == "DEPOSIT");
            Assert.Contains(result.Entries, x => x.Kind == "WITHDRAWAL");
            Assert.False(result.Warnings.Count == 0); // Explicit incompleteness notice
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public async Task Rejects_unapproved_trading_symbol_before_any_private_call()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var http = new HttpClient(new StubBinanceHandler())
            { BaseAddress = new Uri("https://api.binance.com/") };
        var folder = Path.Combine(Path.GetTempPath(), "RAC-ActivityTests-" + Guid.NewGuid().ToString("N"));
        var store = new BinanceSpotCredentialStore(folder);
        try
        {
            var reader = new BinanceSpotAccountClient(http, store);
            var activity = new BinanceSpotActivityClient(http, store, reader);
            await Assert.ThrowsAsync<ArgumentException>(() =>
                activity.FetchAsync("UNSUPPORTEDPAIR", false, CancellationToken.None));
        }
        finally
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void Ledger_is_idempotent_and_owner_isolated_at_database_level()
    {
        using var db = new RacDbContext(
            new DbContextOptionsBuilder<RacDbContext>()
                .UseSqlServer("Server=(local);Database=MetadataOnly;Integrated Security=True;TrustServerCertificate=True")
                .Options);
        var type = db.Model.FindEntityType(typeof(SpotActivity));
        Assert.NotNull(type);
        Assert.Contains(type.GetIndexes(), x => x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual(
            [
                nameof(SpotActivity.OwnerUserId), nameof(SpotActivity.Kind),
                nameof(SpotActivity.Symbol), nameof(SpotActivity.ReferenceId)
            ]));
        Assert.Equal("decimal(28,12)", type.FindProperty(nameof(SpotActivity.Fee))?.GetColumnType());
    }

    private sealed class StubBinanceHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage req, CancellationToken ct)
        {
            Assert.Equal(HttpMethod.Get, req.Method);
            var json = req.RequestUri?.AbsolutePath switch
            {
                "/api/v3/time" => """{"serverTime":1795000000000}""",
                "/sapi/v1/account/apiRestrictions" => """
                  {"enableReading":true,"enableWithdrawals":false,
                  "enableSpotAndMarginTrading":false,"enableMargin":false,
                  "enableFutures":false,"enablePortfolioMarginTrading":false,
                  "enableInternalTransfer":false,"permitsUniversalTransfer":false}
                  """,
                "/api/v3/account" => """{"balances":[]}""",
                "/api/v3/myTrades" => """
                  [{"id":123,"price":"90000.00","qty":"0.001",
                    "quoteQty":"90.00","commission":"0.000001",
                    "commissionAsset":"BTC","time":1795000000000,"isBuyer":true}]
                  """,
                "/sapi/v1/capital/deposit/hisrec" => """
                  [{"id":"deposit-123","coin":"USDT","amount":"5.5",
                    "status":1,"insertTime":1795000000000}]
                  """,
                "/sapi/v1/capital/withdraw/history" => """
                  [{"id":"withdraw-123","coin":"USDT","amount":"1.5",
                    "transactionFee":"0.01","status":6,
                    "applyTime":"2026-11-22 12:00:00"}]
                  """,
                _ => throw new InvalidOperationException("Unexpected endpoint")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
