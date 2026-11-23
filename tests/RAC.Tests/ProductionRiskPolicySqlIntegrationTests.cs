using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

public sealed class ProductionRiskPolicySqlIntegrationTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options)
        : IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext() => new(options);
    }

    [Fact]
    public async Task Policy_proposal_and_independent_review_are_atomic_and_non_trading()
    {
        if (!OperatingSystem.IsWindows()) return;
        var dbName = "RAC_MVP1511_TEST_" + Guid.NewGuid().ToString("N");
        var cs = @"Server=(localdb)\MSSQLLocalDB;Database=" + dbName +
            ";Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer(cs, sql => sql.CommandTimeout(30)).Options;
        try
        {
            await using (var setup = new RacDbContext(options))
                await setup.Database.EnsureCreatedAsync();
            var store = new ProductionRiskPolicyStore(new Factory(options));
            var empty = await store.HistoryAsync();
            Assert.Contains("tradingEnabled = False", empty.ToString());
            var proposed = await store.ProposeAsync(new ProductionRiskLimits(
                100m, 10m, 1m, 10m, 1, 3, 10,
                new HashSet<string>(StringComparer.Ordinal) { "BTCUSDT" }),
                "proposer");
            Assert.Equal("PENDING_REVIEW", proposed.Status);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                store.ReviewAsync(proposed.Id, "proposer",
                    RacRoles.Administrator, "Independent review with notes.", true));
            var reviewed = await store.ReviewAsync(proposed.Id, "reviewer",
                RacRoles.Administrator, "Independent review with complete notes.", true);
            Assert.Equal("REVIEWED_NO_TRADING_PERMISSION", reviewed.Status);
            await Assert.ThrowsAsync<InvalidDataException>(() =>
                store.ReviewAsync(proposed.Id, "another",
                    RacRoles.Administrator, "Duplicate review must not be allowed.", true));
            await using var db = new RacDbContext(options);
            Assert.Equal(1, await db.ProductionRiskPolicies.CountAsync());
            Assert.Equal(2, await db.ProductionRiskPolicyAudits.CountAsync());
            Assert.False(ProductionRiskGate.RealTradingImplementedAndAuthorized);
            var original = await db.ProductionRiskPolicies.AsNoTracking().SingleAsync();
            Assert.Equal(ProductionRiskPolicyStore.Sha256(original.LimitsJson),
                original.LimitsSha256);
        }
        finally
        {
            await using var db = new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
