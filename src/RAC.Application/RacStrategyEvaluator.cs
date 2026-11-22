using RAC.Domain;

namespace RAC.Application;

/// <summary>
/// Deterministic technical signals computed ONLY from closed, contiguous
/// Binance Spot candles. Analysis is observational, not an execution request.
/// </summary>
public static class RacStrategyEvaluator
{
    public const int MinimumCandles = 35;
    public const string Version = "SMA9-21_RSI14_ATR14_V1";

    public static StrategySignal Evaluate(string symbol, IReadOnlyList<MarketCandlePoint> candles,
        DateTimeOffset nowUtc)
    {
        if (!RacMarketSymbols.IsSupported(symbol))
            throw new ArgumentException("Par não monitorado.");
        if (candles.Count == 0)
            return Unavailable(symbol, "Sem candles fechados no banco.");
        if (candles.Any(x => x.Symbol != symbol || x.Close <= 0m || x.High < x.Low ||
            x.CloseUtc <= x.OpenUtc || x.CloseUtc > nowUtc))
            return Unavailable(symbol, "Candles inválidos ou ainda abertos.");

        for (var i = 1; i < candles.Count; i++)
            if (candles[i].OpenUtc <= candles[i - 1].OpenUtc ||
                candles[i].OpenUtc - candles[i - 1].OpenUtc != TimeSpan.FromMinutes(1))
                return Unavailable(symbol, "Candles descontínuos ou fora de ordem.");

        var last = candles[^1];
        if (nowUtc - last.CloseUtc > TimeSpan.FromMinutes(3))
            return Unavailable(symbol, "Dados de mercado desatualizados.", last.Close, last.CloseUtc);
        if (candles.Count < MinimumCandles)
            return Unavailable(symbol, "Histórico insuficiente para indicadores.", last.Close, last.CloseUtc);

        var fast = AverageClose(candles, 9);
        var slow = AverageClose(candles, 21);
        var rsi = Rsi(candles, 14);
        var atr = Atr(candles, 14);
        var action = fast > slow && rsi >= 50m && rsi <= 68m
            ? "COMPRA_OBSERVACIONAL"
            : fast < slow || rsi > 74m ? "SAIDA_OBSERVACIONAL" : "AGUARDAR";
        return new StrategySignal(symbol, "BINANCE_SPOT_PUBLIC", Version,
            "READY", action, last.Close, last.CloseUtc,
            fast, slow, rsi, atr, null, false);
    }

    private static StrategySignal Unavailable(string symbol, string note,
        decimal? price = null, DateTimeOffset? timestamp = null) =>
        new(symbol, "BINANCE_SPOT_PUBLIC", Version, "BLOCKED", "SEM_SINAL",
            price, timestamp, null, null, null, null, note, false);

    public static decimal AverageClose(IReadOnlyList<MarketCandlePoint> candles, int n) =>
        candles.Skip(candles.Count - n).Average(x => x.Close);

    public static decimal Rsi(IReadOnlyList<MarketCandlePoint> candles, int n)
    {
        decimal gains = 0m, losses = 0m;
        for (int i = candles.Count - n; i < candles.Count; i++)
        {
            var change = candles[i].Close - candles[i - 1].Close;
            if (change > 0m) gains += change; else losses -= change;
        }
        if (gains == 0m && losses == 0m) return 50m;
        if (losses == 0m) return 100m;
        return 100m - 100m / (1m + gains / losses);
    }

    public static decimal Atr(IReadOnlyList<MarketCandlePoint> candles, int n)
    {
        decimal total = 0m;
        for (var i = candles.Count - n; i < candles.Count; i++)
        {
            var prior = candles[i - 1].Close;
            var item = candles[i];
            total += Math.Max(item.High - item.Low,
                Math.Max(Math.Abs(item.High - prior), Math.Abs(item.Low - prior)));
        }
        return total / n;
    }
}

public sealed record StrategySignal(string Symbol, string Source,
    string StrategyVersion, string Status, string Action, decimal? PriceUsdt,
    DateTimeOffset? LastClosedUtc, decimal? Sma9, decimal? Sma21,
    decimal? Rsi14, decimal? Atr14, string? Note, bool OrderPlacementEnabled);

public sealed record StrategyBacktest(
    string Symbol, string Source, string StrategyVersion, string Status,
    int CandleCount, int Trades, decimal InitialUsdt, decimal FinalUsdt,
    decimal ReturnPct, decimal MaxDrawdownPct, decimal FeePctPerSide,
    decimal SlippagePctPerSide, string? Note, bool OrderPlacementEnabled);

/// <summary>
/// Long-only, one position, all-in toy comparison. Signal from candle i executes
/// at candle i+1 open; NO same-bar fills. Simulation costs are assumed, not historical.
/// </summary>
public static class RacBacktest
{
    public static StrategyBacktest Run(string symbol, IReadOnlyList<MarketCandlePoint> data,
        DateTimeOffset observedUtc)
    {
        const decimal fee = 0.1m, slip = 0.05m;
        if (!RacMarketSymbols.IsSupported(symbol))
            throw new ArgumentException("Par não monitorado.");
        if (data.Count < RacStrategyEvaluator.MinimumCandles + 5 ||
            data.Count > 2000)
            return Blocked(symbol, data.Count, "Histórico insuficiente ou acima do limite.");
        // Run indicators on a clean copy of the SAME historical series; use a
        // synthetic 'as-of' clock for past candles and a real clock for final freshness.
        var all = RacStrategyEvaluator.Evaluate(symbol, data, observedUtc);
        if (all.Status != "READY")
            return Blocked(symbol, data.Count, all.Note ?? "Dados inválidos.");
        decimal cash = 100m, quantity = 0m, peak = 100m, maxDd = 0m;
        int completedTrades = 0;
        for (int i = RacStrategyEvaluator.MinimumCandles - 1; i < data.Count - 1; i++)
        {
            var context = data.Take(i + 1).ToArray();
            var signal = RacStrategyEvaluator.Evaluate(
                symbol, context, data[i].CloseUtc);
            var nextOpen = data[i + 1].Open;
            if (signal.Status != "READY" || nextOpen <= 0m) continue;
            if (quantity == 0m && signal.Action == "COMPRA_OBSERVACIONAL")
            {
                var executionPrice = nextOpen * (1m + slip / 100m);
                quantity = cash * (1m - fee / 100m) / executionPrice;
                cash = 0m;
            }
            else if (quantity > 0m && signal.Action == "SAIDA_OBSERVACIONAL")
            {
                cash = quantity * nextOpen * (1m - slip / 100m) * (1m - fee / 100m);
                quantity = 0m;
                completedTrades++;
            }
            var equity = cash + quantity * data[i + 1].Close;
            peak = Math.Max(peak, equity);
            if (peak > 0m) maxDd = Math.Max(maxDd, (peak - equity) / peak * 100m);
        }
        var finalEquity = cash + quantity * data[^1].Close *
            (1m - slip / 100m) * (1m - fee / 100m);
        peak = Math.Max(peak, finalEquity);
        maxDd = Math.Max(maxDd, (peak - finalEquity) / peak * 100m);
        return new StrategyBacktest(symbol, "BINANCE_SPOT_PUBLIC", RacStrategyEvaluator.Version,
            "COMPLETE", data.Count, completedTrades, 100m,
            Math.Round(finalEquity, 6), Math.Round(finalEquity - 100m, 4),
            Math.Round(maxDd, 4), fee, slip,
            "Backtest ilustrativo in-sample: não valida lucro futuro, tamanho mínimo, liquidez ou taxas reais.",
            false);
    }

    private static StrategyBacktest Blocked(string symbol, int count, string note) =>
        new(symbol, "BINANCE_SPOT_PUBLIC", RacStrategyEvaluator.Version,
            "BLOCKED", count, 0, 100m, 100m, 0m, 0m,
            0.1m, 0.05m, note, false);
}
