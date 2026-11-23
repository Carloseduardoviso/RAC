using RAC.Application;
using RAC.Domain;

namespace RAC.Tests;

public sealed class RacValidationChainTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 23, 17, 0, 0, TimeSpan.Zero);

    // Fecha em zigue-zague sobre uma deriva: sobe na média, mas com quedas,
    // para o RSI ficar entre 50 e 68 e o avaliador emitir COMPRA_OBSERVACIONAL.
    private static MarketCandlePoint[] Series(int count = 60, decimal step = 0.05m,
        decimal lastQuoteVolume = 500m)
    {
        const decimal zig = 0.1m;
        var first = Now.AddMinutes(-count);
        var previousClose = 100m;
        return Enumerable.Range(0, count).Select(i =>
        {
            var close = 100m + i * step + (i % 2 == 0 ? zig : -zig);
            var open = previousClose;
            previousClose = close;
            var start = first.AddMinutes(i);
            return new MarketCandlePoint("BTCUSDT", start, start.AddMinutes(1), open,
                Math.Max(open, close) + 0.1m, Math.Min(open, close) - 0.1m, close, 5m,
                i == count - 1 ? lastQuoteVolume : 500m, 10);
        }).ToArray();
    }

    private static ValidationContext Context(MarketCandlePoint[] candles,
        decimal? quote = null, int openPositions = 0, decimal equity = 1000m,
        decimal dayStart = 1000m, decimal peak = 1000m)
    {
        var signal = RacStrategyEvaluator.Evaluate("BTCUSDT", candles, Now);
        return new ValidationContext("BTCUSDT", candles, signal, Now,
            quote ?? signal.PriceUsdt, 5m, equity, equity, dayStart, peak, 0m, openPositions);
    }

    private sealed class FixedCalendar(params RacNewsEvent[] events) : IRacNewsCalendar
    {
        public IReadOnlyList<RacNewsEvent>? EventsAround(string symbol, DateTimeOffset nowUtc) => events;
    }

    [Fact]
    public void Uptrend_with_normal_volume_is_approved_and_news_is_unverified()
    {
        var candles = Series();
        Assert.Equal("COMPRA_OBSERVACIONAL",
            RacStrategyEvaluator.Evaluate("BTCUSDT", candles, Now).Action);
        var chain = RacValidationChain.CreateDefault();
        var result = chain.Run(Context(candles));
        Assert.True(result.Approved, result.FirstBlockReason);
        Assert.Equal(new[] { "noticias" }, result.Unverified);
        Assert.Equal(5, result.Results.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, result.Results.Select(x => x.Order));
    }

    [Fact]
    public void Downtrend_is_blocked_by_trend_with_a_reason()
    {
        var chain = RacValidationChain.CreateDefault();
        var result = chain.Run(Context(Series(step: -0.05m)));
        Assert.False(result.Approved);
        Assert.Equal("tendencia", result.FirstBlockedBy);
        Assert.False(string.IsNullOrWhiteSpace(result.FirstBlockReason));
    }

    [Fact]
    public void Weak_volume_is_blocked_by_volume()
    {
        var chain = RacValidationChain.CreateDefault();
        var result = chain.Run(Context(Series(lastQuoteVolume: 100m)));
        Assert.False(result.Approved);
        Assert.Equal("volume", result.FirstBlockedBy);
    }

    [Fact]
    public void High_impact_news_inside_the_window_blocks()
    {
        var calendar = new FixedCalendar(new RacNewsEvent("CPI", Now.AddMinutes(4), "high"));
        var chain = RacValidationChain.CreateDefault(calendar);
        var result = chain.Run(Context(Series()));
        Assert.False(result.Approved);
        Assert.Equal("noticias", result.FirstBlockedBy);
        Assert.Empty(result.Unverified);
    }

    [Fact]
    public void Low_impact_or_distant_news_is_approved()
    {
        var calendar = new FixedCalendar(
            new RacNewsEvent("Leilão", Now.AddMinutes(3), "low"),
            new RacNewsEvent("CPI", Now.AddMinutes(45), "high"));
        var result = RacValidationChain.CreateDefault(calendar).Run(Context(Series()));
        Assert.True(result.Approved, result.FirstBlockReason);
    }

    [Fact]
    public void Open_position_limit_and_daily_loss_block_by_risk()
    {
        var chain = RacValidationChain.CreateDefault();
        var full = chain.Run(Context(Series(), openPositions: PaperRisk.MaxOpenPositions));
        Assert.Equal("risco", full.FirstBlockedBy);
        var losing = chain.Run(Context(Series(), equity: 980m, dayStart: 1000m));
        Assert.Equal("risco", losing.FirstBlockedBy);
    }

    [Fact]
    public void Quote_far_from_closed_candle_is_blocked_by_entry()
    {
        var candles = Series();
        var signal = RacStrategyEvaluator.Evaluate("BTCUSDT", candles, Now);
        var result = RacValidationChain.CreateDefault()
            .Run(Context(candles, quote: signal.PriceUsdt!.Value * 1.05m));
        Assert.False(result.Approved);
        Assert.Equal("entrada", result.FirstBlockedBy);
    }

    [Fact]
    public void Unavailable_blocks_only_when_configured()
    {
        var context = Context(Series());
        var lenient = RacValidationChain.CreateDefault(null, new RacValidationOptions());
        var strict = RacValidationChain.CreateDefault(null,
            new RacValidationOptions { TreatUnavailableAsBlocked = true });
        Assert.True(lenient.Run(context).Approved);
        var blocked = strict.Run(context);
        Assert.False(blocked.Approved);
        Assert.Equal("noticias", blocked.FirstBlockedBy);
    }

    [Fact]
    public void A_throwing_validator_fails_closed()
    {
        var chain = new RacValidationChain([new ThrowingValidator()]);
        var result = chain.Run(Context(Series()));
        Assert.False(result.Approved);
        Assert.Equal("boom", result.FirstBlockedBy);
    }

    [Fact]
    public void Shadow_mode_is_the_default()
    {
        Assert.False(RacValidationChain.CreateDefault().Options.Enforce);
    }

    private sealed class ThrowingValidator : IRacValidator
    {
        public string Id => "boom";
        public int Order => 1;
        public string AgentName => "Teste";
        public ValidatorResult Validate(ValidationContext context) =>
            throw new InvalidOperationException("falha simulada");
    }
}
