using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RAC.Api.Services;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

/// <summary>Disposable LocalDB only. Never connects to the running RAC database or Binance.</summary>
public sealed class PaperSqlRecoveryIntegrationTests
{
    [Fact]
    public async Task Restart_pauses_paper_preserves_ledger_and_monitor_is_idempotent()
    {
        if (!OperatingSystem.IsWindows()) return;
        var name = "RAC_PAPER_RECOVERY_TEST_" + Guid.NewGuid().ToString("N");
        var connection = @"Server=(localdb)\MSSQLLocalDB;Database=" + name +
            ";Integrated Security=True;TrustServerCertificate=True;Encrypt=True";
        var options = new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer(connection, sql => sql.CommandTimeout(30)).Options;
        var now = DateTimeOffset.UtcNow;
        var symbol = "BTCUSDT";
        try
        {
            await using (var seed = new RacDbContext(options))
            {
                await seed.Database.EnsureCreatedAsync();
                seed.PaperAccounts.Add(new PaperAccount { Id = 1, Running = true,
                    LastCycleUtc = now.AddMinutes(-1), EntriesToday = 19 });
                seed.PaperAgentCursors.Add(new PaperAgentCursor { Symbol = symbol,
                    LastCandleUtc = now.AddMinutes(-1), LastSignal = "COMPRA", Status = "READY" });
                seed.PaperDecisionAudits.Add(new PaperDecisionAudit { Symbol = symbol,
                    CandleClosedUtc = now.AddMinutes(-1), RecordedUtc = now,
                    Signal = "COMPRA", Outcome = "PAPER_BUY", Actor = "RAC" });
                seed.PaperFills.Add(new PaperFill { Symbol = symbol, Side = "BUY",
                    Reason = "SMA_RSI", Quantity = .001m, PriceBrl = 100m,
                    FeeBrl = .01m, ExecutedUtc = now,
                    SignalClosedUtc = now.AddMinutes(-1) });
                await seed.SaveChangesAsync();
            }
            var factory = new DisposableFactory(options);
            using var client = new HttpClient(new NoNetwork());
            var paper = new RacPaperTradingService(factory,
                new BinancePortfolioPricesClient(client),
                NullLogger<RacPaperTradingService>.Instance);
            var watcher = new RacPaperMonitoringService(factory, paper,
                NullLogger<RacPaperMonitoringService>.Instance);
            await watcher.ObserveAsync(CancellationToken.None);
            await watcher.ObserveAsync(CancellationToken.None);
            // The batch validation is read-only, preserves every trade, and
            // honestly reports insufficient candles rather than fake returns.
            var validationService = new RacStrategyService(factory);
            using (var validation = System.Text.Json.JsonDocument.Parse(
                System.Text.Json.JsonSerializer.Serialize(
                    await validationService.ValidationAsync(CancellationToken.None))))
            {
                var root = validation.RootElement;
                Assert.Equal("READ_ONLY_VALIDATION", root.GetProperty("mode").GetString());
                var risk = root.GetProperty("riskReview");
                Assert.False(risk.GetProperty("financialLimitsChanged").GetBoolean());
                Assert.False(risk.GetProperty("riskLocked").GetBoolean());
                Assert.Equal(0m, risk.GetProperty("dailyLossPct").GetDecimal());
                Assert.Equal(1m, risk.GetProperty("dailyLossLimitPct").GetDecimal());
                Assert.Equal(0, risk.GetProperty("totalClosedSells").GetInt32());
                Assert.Equal(System.Text.Json.JsonValueKind.Null, risk.GetProperty("closedSellLossRatePct").ValueKind);
                Assert.Empty(risk.GetProperty("exitReasons").EnumerateArray());
                Assert.Empty(risk.GetProperty("openPositions").EnumerateArray());
                Assert.False(root.GetProperty("orderPlacementEnabled").GetBoolean());
                Assert.False(root.GetProperty("simulationModified").GetBoolean());
                Assert.Equal(0.01m, root.GetProperty("assumptions").GetProperty("halfSpreadPctPerSide").GetDecimal());
                Assert.True(root.GetProperty("assumptions").GetProperty("spreadModeledSeparately").GetBoolean());
                Assert.Equal("NEXT_CANDLE_OPEN", root.GetProperty("assumptions").GetProperty("execution").GetString());
                Assert.Equal(12, root.GetProperty("agents").GetArrayLength());
                Assert.All(root.GetProperty("agents").EnumerateArray(), x =>
                    Assert.Equal("INSUFFICIENT_DATA", x.GetProperty("verdict").GetString()));
                var btcValidation = root.GetProperty("agents").EnumerateArray()
                    .Single(x => x.GetProperty("symbol").GetString() == "BTCUSDT");
                Assert.Equal(1, btcValidation.GetProperty("paper").GetProperty("buys").GetInt32());
                Assert.False(btcValidation.GetProperty("comparablePnl").GetBoolean());
            }
            var reportJson = System.Text.Json.JsonSerializer.Serialize(
                await watcher.ReportAsync(CancellationToken.None));
            using (var report = System.Text.Json.JsonDocument.Parse(reportJson))
            {
                var cycle = report.RootElement.GetProperty("cycleHealth");
                Assert.Equal("PAUSED", cycle.GetProperty("status").GetString());
                var windows = report.RootElement.GetProperty("comparisonWindow");
                Assert.Equal(7, windows.GetProperty("daysPerPeriod").GetInt32());
                var historical = report.RootElement.GetProperty("historicalComparison");
                Assert.Equal(12, historical.GetArrayLength());
                var btcHistorical = historical.EnumerateArray().Single(x =>
                    x.GetProperty("symbol").GetString() == "BTCUSDT");
                Assert.Equal(1, btcHistorical.GetProperty("currentBuys").GetInt32());
                Assert.Equal(0, btcHistorical.GetProperty("previousBuys").GetInt32());
                Assert.Equal(System.Text.Json.JsonValueKind.Null,
                    btcHistorical.GetProperty("realizedPnlChangePct").ValueKind);
                var agentStates = report.RootElement.GetProperty("agentStates");
                Assert.Equal(12, agentStates.GetArrayLength());
                var btcHealth = agentStates.EnumerateArray().Single(agent =>
                    agent.GetProperty("Symbol").GetString() == "BTCUSDT");
                Assert.Equal("READY", btcHealth.GetProperty("diagnosticStatus").GetString());
                Assert.True(btcHealth.GetProperty("healthy").GetBoolean());
                Assert.True(btcHealth.GetProperty("candleAgeSeconds").GetDouble() >= 0d);
                var performance = report.RootElement.GetProperty("agentPerformance");
                Assert.Equal(12, performance.GetArrayLength());
                var btc = performance.EnumerateArray().Single(agent =>
                    agent.GetProperty("symbol").GetString() == "BTCUSDT");
                Assert.Equal(1, btc.GetProperty("buys").GetInt32());
                Assert.Equal(0, btc.GetProperty("sells").GetInt32());
                Assert.Equal(1, btc.GetProperty("decisions").GetInt32());
                Assert.Equal(0m, btc.GetProperty("realizedPnlBrl").GetDecimal());
                Assert.Equal(.01m, btc.GetProperty("feesBrl").GetDecimal());
                Assert.Equal(0m, btc.GetProperty("winRatePct").GetDecimal());
                Assert.Equal(0m, btc.GetProperty("exposurePct").GetDecimal());
                Assert.Equal(0m, report.RootElement.GetProperty("performance")
                    .GetProperty("exposureBrl").GetDecimal());
                Assert.Equal(0m, report.RootElement.GetProperty("performance")
                    .GetProperty("winRatePct").GetDecimal());
                Assert.Contains(agentStates.EnumerateArray(), agent =>
                    agent.GetProperty("Symbol").GetString() == "BTCUSDT" &&
                    agent.GetProperty("healthy").GetBoolean());
            }
            await using (var restored = new RacDbContext(options))
            {
                var account = await restored.PaperAccounts.AsNoTracking().SingleAsync();
                Assert.False(account.Running); // restore must always pause
                Assert.False(account.RiskLocked);
                Assert.Equal(19, account.EntriesToday); // no daily count cap
                Assert.Equal(1, await restored.PaperFills.CountAsync());
                Assert.Equal(1, await restored.PaperDecisionAudits.CountAsync());
                Assert.Equal(1, await restored.PaperAgentCursors.CountAsync());
                Assert.Equal(1, await restored.PaperMonitorSamples.CountAsync());
                Assert.Equal(0, await restored.TestnetOrders.CountAsync());
                // SQL prevents replaying the same candle's paper decision.
                restored.PaperDecisionAudits.Add(new PaperDecisionAudit { Symbol = symbol,
                    CandleClosedUtc = now.AddMinutes(-1), RecordedUtc = now,
                    Signal = "COMPRA", Outcome = "PAPER_BUY", Actor = "RAC" });
                await Assert.ThrowsAsync<DbUpdateException>(() => restored.SaveChangesAsync());
            }
        }
        finally
        {
            await using var cleanup = new RacDbContext(options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private sealed class DisposableFactory(DbContextOptions<RacDbContext> options)
        : IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext() => new(options);
    }
    private sealed class NoNetwork : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken ct) => throw new InvalidOperationException("Network forbidden in test");
    }
}
