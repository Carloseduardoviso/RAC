using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

/// <summary>Uses a disposable SQL LocalDB; never opens the operational RAC database.</summary>
public sealed class ProductionRiskPolicyHardeningTests
{
    private sealed class Factory(DbContextOptions<RacDbContext> options)
        : IDbContextFactory<RacDbContext>
    {
        public RacDbContext CreateDbContext() => new(options);
    }

    private static ProductionRiskLimits Limits() => new(
        100m, 10m, 1m, 10m, 1, 3, 10,
        new HashSet<string>(StringComparer.Ordinal) { "BTCUSDT" });

    [Fact]
    public async Task History_checks_integrity_and_exposes_monitoring_counts()
    {
        if (!OperatingSystem.IsWindows()) return;
        var database = "RAC_MVP1512_TEST_" + Guid.NewGuid().ToString("N");
        var cs = @"Server=(localdb)\MSSQLLocalDB;Database=" + database +
            ";Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer(cs, sql => sql.CommandTimeout(30)).Options;
        try
        {
            await using (var db = new RacDbContext(options))
                await db.Database.EnsureCreatedAsync();
            var store = new ProductionRiskPolicyStore(new Factory(options));
            var first = await store.ProposeAsync(Limits(), "alice");
            var second = await store.ProposeAsync(Limits(), "bob");
            await store.ReviewAsync(second.Id, "carol", RacRoles.Administrator,
                "Independent review with all checks completed.", false);
            var data = System.Text.Json.JsonSerializer.Serialize(
                await store.HistoryAsync());
            using var parsed = System.Text.Json.JsonDocument.Parse(data);
            var security = parsed.RootElement.GetProperty("security");
            Assert.Equal(1, security.GetProperty("pendingReview").GetInt32());
            Assert.Equal(1, security.GetProperty("rejected").GetInt32());
            Assert.Equal(0, security.GetProperty("reviewedDocumentOnly").GetInt32());
            Assert.False(security.GetProperty("orderPlacementEnabled").GetBoolean());
            await using (var db = new RacDbContext(options))
            {
                var record = await db.ProductionRiskPolicies.SingleAsync(x => x.Id == first.Id);
                record.LimitsJson += " ";
                await db.SaveChangesAsync();
            }
            await Assert.ThrowsAsync<InvalidDataException>(
                () => store.HistoryAsync());
            Assert.False(ProductionRiskGate.RealTradingImplementedAndAuthorized);
        }
        finally
        {
            await using var db = new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Fact]
    public async Task Simultaneous_review_allows_only_one_audit()
    {
        if (!OperatingSystem.IsWindows()) return;
        var database = "RAC_MVP1512_RACE_" + Guid.NewGuid().ToString("N");
        var cs = @"Server=(localdb)\MSSQLLocalDB;Database=" + database +
            ";Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer(cs, sql => sql.CommandTimeout(30)).Options;
        try
        {
            await using (var db = new RacDbContext(options))
                await db.Database.EnsureCreatedAsync();
            var store = new ProductionRiskPolicyStore(new Factory(options));
            var proposal = await store.ProposeAsync(Limits(), "alice");
            var calls = new[] { "bob", "carol" }.Select(async reviewer =>
            {
                try
                {
                    await store.ReviewAsync(proposal.Id, reviewer,
                        RacRoles.Administrator,
                        "Independent review of policy with full context.", true);
                    return true;
                }
                catch (Exception ex) when (ex is InvalidDataException or
                    DbUpdateException or Microsoft.Data.SqlClient.SqlException)
                {
                    return false;
                }
            }).ToArray();
            var results = await Task.WhenAll(calls);
            Assert.Equal(1, results.Count(x => x));
            await using var verify = new RacDbContext(options);
            Assert.Equal(2, await verify.ProductionRiskPolicyAudits.CountAsync());
            Assert.Equal("REVIEWED_NO_TRADING_PERMISSION",
                (await verify.ProductionRiskPolicies.SingleAsync()).Status);
        }
        finally
        {
            await using var db = new RacDbContext(options);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
