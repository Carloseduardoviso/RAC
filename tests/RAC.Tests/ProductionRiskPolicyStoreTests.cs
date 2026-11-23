using RAC.Application;
using RAC.Infrastructure;

namespace RAC.Tests;

public sealed class ProductionRiskPolicyStoreTests
{
    private static ProductionRiskLimits Limits() => new(
        100m, 20m, 1m, 10m, 1, 3, 10,
        new HashSet<string>(StringComparer.Ordinal) { "ETHUSDT", "BTCUSDT" });

    [Fact]
    public void Canonical_policy_has_stable_hash_regardless_of_symbol_order()
    {
        var first = ProductionRiskPolicyStore.CanonicalLimits(Limits());
        var second = ProductionRiskPolicyStore.CanonicalLimits(
            Limits() with { AllowedSymbols = new HashSet<string>(
                StringComparer.Ordinal) { "BTCUSDT", "ETHUSDT" } });
        Assert.Equal(first, second);
        Assert.Equal(64, ProductionRiskPolicyStore.Sha256(first).Length);
        Assert.Equal(ProductionRiskPolicyStore.Sha256(first),
            ProductionRiskPolicyStore.Sha256(second));
        Assert.False(ProductionRiskGate.RealTradingImplementedAndAuthorized);
    }

    [Fact]
    public void Unapproved_or_incomplete_limits_cannot_be_persisted()
    {
        Assert.Throws<InvalidDataException>(() =>
            ProductionRiskPolicyStore.CanonicalLimits(ProductionRiskLimits.Unapproved));
        Assert.Throws<InvalidDataException>(() =>
            ProductionRiskPolicyStore.CanonicalLimits(Limits() with {
                MaximumOrdersPerDay = 0 }));
    }

    [Fact]
    public void Unsupported_symbol_cannot_be_persisted()
    {
        Assert.Throws<InvalidDataException>(() =>
            ProductionRiskPolicyStore.CanonicalLimits(Limits() with {
                AllowedSymbols = new HashSet<string>(StringComparer.Ordinal)
                {
                    "BTCUSDT", "FAKEUSDT"
                }}));
    }

    [Theory]
    [InlineData("1000001", "20", 1, 3, 10)]
    [InlineData("100", "20", 101, 3, 10)]
    [InlineData("100", "20", 1, 10001, 10)]
    [InlineData("100", "20", 1, 3, 100001)]
    public void Excessive_operational_limits_cannot_be_persisted(
        string capital, string order, int positions, int hourly, int daily)
    {
        Assert.Throws<InvalidDataException>(() =>
            ProductionRiskPolicyStore.CanonicalLimits(new ProductionRiskLimits(
                decimal.Parse(capital), decimal.Parse(order), 1m, 10m,
                positions, hourly, daily,
                new HashSet<string>(StringComparer.Ordinal) { "BTCUSDT" })));
    }

    [Theory]
    [InlineData("alice", "alice", "Administrator", "Review with complete details is required.")]
    [InlineData("Alice", " alice ", "Administrator", "Review with complete details is required.")]
    [InlineData("alice", "bob", "VIEWER", "Review with complete details is required.")]
    [InlineData("alice", "bob", "Administrator", "too short")]
    public void Review_requires_separate_administrator_and_reason(
        string proposer, string reviewer, string role, string reason)
    {
        Assert.Throws<InvalidOperationException>(() =>
            ProductionRiskPolicyStore.ValidateReview(
                proposer, reviewer, role, reason));
    }

    [Fact]
    public void Distinct_administrator_review_is_record_only()
    {
        ProductionRiskPolicyStore.ValidateReview("alice", "bob",
            "Administrator", "Independent review of risk policy proposal.");
        Assert.Equal("REAL_TRADING_DISABLED_IN_SOURCE",
            ProductionRiskGate.Validate(Limits(), null, "BTCUSDT",
                "BUY", 10m, DateTimeOffset.UtcNow));
    }
}
