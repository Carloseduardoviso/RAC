using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RAC.Application;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

public sealed class SpotPortfolioValuationTests
{
    private static DateTimeOffset Now => new(2026, 11, 16, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Tiny_real_brl_balance_is_preserved_without_rounding()
    {
        var result = SpotValuationCalculator.Calculate(
            [new SpotAssetAmount("BRL", 0.00695586m, 0m)],
            new Dictionary<string, decimal> { ["USDTBRL"] = 5.31m }, Now);
        Assert.True(result.CompleteValuation);
        Assert.Equal(0.00695586m, result.TotalEstimatedBrl);
        Assert.Equal(0.00695586m, result.ValuedSubsetBrl);
        Assert.Equal("BRL_DIRETO", Assert.Single(result.Holdings).PriceSource);
    }

    [Fact]
    public void Uses_spot_prices_and_usdtbrl_to_value_non_brl_assets()
    {
        var result = SpotValuationCalculator.Calculate(
        [
            new SpotAssetAmount("BTC", 0.01m, 0m),
            new SpotAssetAmount("USDT", 10m, 0m),
            new SpotAssetAmount("BRL", 5m, 0m)
        ], new Dictionary<string, decimal>
        {
            ["BTCUSDT"] = 90000m,
            ["USDTBRL"] = 5.2m
        }, Now);
        Assert.True(result.CompleteValuation);
        Assert.Equal(4737m, result.TotalEstimatedBrl);
        Assert.Equal(3, result.PricedAssetCount);
        Assert.Equal("BINANCE_VIA_USDTBRL",
            result.Holdings.Single(x => x.Asset == "BTC").PriceSource);
    }

    [Fact]
    public void Unknown_assets_never_receive_fabricated_prices_or_total()
    {
        var result = SpotValuationCalculator.Calculate(
        [
            new SpotAssetAmount("BRL", 12m, 0m),
            new SpotAssetAmount("UNKNOWN", 4m, 1m)
        ], new Dictionary<string, decimal> { ["USDTBRL"] = 5m }, Now);
        Assert.False(result.CompleteValuation);
        Assert.Null(result.TotalEstimatedBrl);
        Assert.Equal(12m, result.ValuedSubsetBrl);
        Assert.Equal(1, result.UnpricedAssetCount);
        Assert.Null(result.Holdings.Single(x => x.Asset == "UNKNOWN").EstimatedValueBrl);
    }

    [Fact]
    public void Direct_brl_market_is_preferred_when_available()
    {
        var result = SpotValuationCalculator.Calculate(
            [new SpotAssetAmount("SOL", 2m, 0m)],
            new Dictionary<string, decimal>
            {
                ["SOLBRL"] = 200m, ["SOLUSDT"] = 30m, ["USDTBRL"] = 5m
            }, Now);
        Assert.Equal(400m, result.TotalEstimatedBrl);
        Assert.Equal("BINANCE_PAR_BRL", result.Holdings[0].PriceSource);
    }

    [Fact]
    public void Locked_assets_are_included_in_spot_value_but_not_counted_twice()
    {
        var result = SpotValuationCalculator.Calculate(
            [new SpotAssetAmount("USDT", 4m, 6m)],
            new Dictionary<string, decimal> { ["USDTBRL"] = 5m }, Now);
        Assert.Equal(50m, result.TotalEstimatedBrl);
        Assert.Single(result.Holdings);
        Assert.Equal(6m, result.Holdings[0].Locked);
    }

    [Fact]
    public void Empty_wallet_is_zero_not_an_estimated_profit()
    {
        var result = SpotValuationCalculator.Calculate(
            [], new Dictionary<string, decimal> { ["USDTBRL"] = 5m }, Now);
        Assert.True(result.CompleteValuation);
        Assert.Equal(0m, result.TotalEstimatedBrl);
    }

    [Fact]
    public void Public_price_parser_handles_market_symbols_without_api_key()
    {
        using var doc = JsonDocument.Parse(
            """[{"symbol":"USDTBRL","price":"5.243"},{"symbol":"BTCUSDT","price":"90000.125"}]""");
        var batch = BinancePortfolioPricesClient.ParsePrices(doc.RootElement, Now);
        Assert.Equal(Now, batch.ObservedUtc);
        Assert.Equal(5.243m, batch.Prices["USDTBRL"]);
        Assert.Equal(90000.125m, batch.Prices["BTCUSDT"]);
    }

    [Fact]
    public void Portfolio_tables_have_owner_indexes_and_precise_decimal_columns()
    {
        using var db = new RacDbContext(
            new DbContextOptionsBuilder<RacDbContext>()
                .UseSqlServer("Server=(local);Database=MetadataOnly;Integrated Security=True;TrustServerCertificate=True")
                .Options);
        var snapshots = db.Model.FindEntityType(typeof(SpotPortfolioSnapshot));
        var holdings = db.Model.FindEntityType(typeof(SpotPortfolioHolding));
        Assert.NotNull(snapshots);
        Assert.NotNull(holdings);
        Assert.Contains(snapshots.GetIndexes(), idx =>
            idx.Properties.Select(p => p.Name).SequenceEqual(
                [nameof(SpotPortfolioSnapshot.OwnerUserId), nameof(SpotPortfolioSnapshot.WalletObservedUtc)]));
        Assert.Contains(holdings.GetIndexes(), idx => idx.IsUnique &&
            idx.Properties.Select(p => p.Name).SequenceEqual(
                [nameof(SpotPortfolioHolding.SnapshotId), nameof(SpotPortfolioHolding.Asset)]));
    }
}
