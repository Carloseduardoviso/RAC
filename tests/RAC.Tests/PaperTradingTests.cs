using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

public sealed class PaperTradingTests
{
    [Fact]
    public void Requested_virtual_risk_policy_is_exact_and_separate_from_real_wallet()
    {
        Assert.Equal(100m, PaperRisk.StartingCapitalBrl);
        Assert.Equal(200m, PaperRisk.MonthlyVirtualContributionBrl);
        Assert.Equal(0.5m, PaperRisk.MaxRiskPerTradePct);
        Assert.Equal(1m, PaperRisk.DailyLossLimitPct);
        Assert.Equal(10m, PaperRisk.MaxDrawdownPct);
        Assert.Equal(100m, PaperRisk.ReinvestmentPercent);
        Assert.Equal(1, PaperRisk.MaxOpenPositions);
        Assert.Equal(12, RacMarketSymbols.Supported.Length);
    }

    [Fact]
    public void Daily_and_cumulative_limits_stop_execution()
    {
        Assert.NotNull(PaperRisk.LockReason(99m, 100m, 100m));
        Assert.NotNull(PaperRisk.LockReason(95m, 100m, 110m));
    }

    [Fact]
    public void Loss_below_the_bound_does_not_trip_circuit_breaker()
    {
        Assert.Null(PaperRisk.LockReason(99.9m, 100m, 100m));
    }

    [Fact]
    public void Sizing_never_exceeds_risk_or_cash_or_total_allocation()
    {
        var qty = PaperRisk.Quantity(100m, 100m, 10m, 0.5m, 0m, 0, 0);
        Assert.True(qty > 0m);
        Assert.True(qty * 10m <= 20m);
        Assert.True(qty * 1m <= 0.5m); // 2 ATR stop = R$ 1
        Assert.True(qty * 10m * 1.001m <= 100m);
    }

    [Fact]
    public void Exhausted_limits_and_invalid_market_data_refuse_new_positions()
    {
        Assert.Equal(0m, PaperRisk.Quantity(100m, 100m, 10m, 0.5m, 0m, 1, 0));
        Assert.Equal(0m, PaperRisk.Quantity(100m, 100m, 10m, 0.5m, 0m, 0, 3));
        Assert.Equal(0m, PaperRisk.Quantity(100m, 100m, 0m, 0.5m, 0m, 0, 0));
        Assert.Equal(0m, PaperRisk.Quantity(100m, 100m, 10m, 0m, 0m, 0, 0));
        Assert.Equal(0m, PaperRisk.Quantity(100m, 100m, 10m, 0.5m, 40m, 0, 0));
    }

    [Fact]
    public void Separate_sql_tables_have_persistent_unique_positions_and_candle_keys()
    {
        using var db = new RacDbContext(new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer("Server=(local);Database=MetadataOnly;Integrated Security=True;TrustServerCertificate=True")
            .Options);
        var model = db.Model;
        var account = model.FindEntityType(typeof(PaperAccount));
        Assert.NotNull(account);
        Assert.True(account.FindProperty(nameof(PaperAccount.RowVersion))!.IsConcurrencyToken);
        var pos = model.FindEntityType(typeof(PaperPosition));
        Assert.NotNull(pos);
        Assert.Contains(pos.GetIndexes(), x => x.IsUnique &&
            x.Properties.Single().Name == nameof(PaperPosition.Symbol));
        var fills = model.FindEntityType(typeof(PaperFill));
        Assert.NotNull(fills);
        Assert.Contains(fills.GetIndexes(), x => x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual(
            [nameof(PaperFill.Symbol), nameof(PaperFill.Side), nameof(PaperFill.SignalClosedUtc)]));
        Assert.NotNull(model.FindEntityType(typeof(PaperAgentCursor)));
    }
}
