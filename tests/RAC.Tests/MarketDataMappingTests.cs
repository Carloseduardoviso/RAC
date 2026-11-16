using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

public sealed class MarketDataMappingTests
{
    [Fact]
    public void MarketQuotes_have_one_record_per_symbol()
    {
        using var db = CreateContext();
        var quote = db.Model.FindEntityType(typeof(MarketQuote));
        Assert.NotNull(quote);
        Assert.Equal(nameof(MarketQuote.Symbol), quote.FindPrimaryKey()!.Properties.Single().Name);
    }

    [Fact]
    public void MinuteCandles_have_a_unique_symbol_interval_and_open_time()
    {
        using var db = CreateContext();
        var candles = db.Model.FindEntityType(typeof(MarketCandle));
        Assert.NotNull(candles);
        Assert.Contains(candles.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(
                [nameof(MarketCandle.Symbol), nameof(MarketCandle.Interval),
                 nameof(MarketCandle.OpenUtc)]));
    }

    private static RacDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer("Server=(local);Database=MetadataOnly;Integrated Security=True;TrustServerCertificate=True")
            .Options);
}
