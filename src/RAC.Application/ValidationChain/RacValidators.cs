using RAC.Domain;

namespace RAC.Application;

// Limiares abaixo são pontos de partida NÃO validados estatisticamente.
// Rode a cadeia em modo sombra e compare antes de ativar Enforce.

/// <summary>01 Tendência: médias a favor da compra e inclinação da média lenta positiva.</summary>
public sealed class TrendValidator : IRacValidator
{
    public const int SlopeLookbackCandles = 5;
    public string Id => "tendencia";
    public int Order => 1;
    public string AgentName => "Lia";

    public ValidatorResult Validate(ValidationContext c)
    {
        var minimum = 21 + SlopeLookbackCandles;
        if (c.Candles.Count < minimum || c.Signal.Sma9 is null || c.Signal.Sma21 is null)
            return Make(ValidatorVerdict.Unavailable, "Histórico insuficiente para medir a tendência.", null);
        var slowNow = c.Signal.Sma21.Value;
        var slowBefore = c.Candles.Skip(c.Candles.Count - minimum).Take(21).Average(x => x.Close);
        var separation = slowNow == 0m ? 0m : (c.Signal.Sma9.Value - slowNow) / slowNow * 100m;
        if (c.Signal.Sma9 <= c.Signal.Sma21)
            return Make(ValidatorVerdict.Blocked, "Média rápida abaixo da lenta: sem tendência de alta.", separation);
        if (slowNow <= slowBefore)
            return Make(ValidatorVerdict.Blocked, "Média lenta sem inclinação positiva.", separation);
        return Make(ValidatorVerdict.Approved,
            $"Média rápida {separation:0.###}% acima da lenta, com inclinação positiva.", separation);
    }

    private ValidatorResult Make(ValidatorVerdict v, string reason, decimal? score) =>
        new(Id, Order, AgentName, v, reason, score);
}

/// <summary>02 Volume: o último candle não pode ter volume muito abaixo da média recente.</summary>
public sealed class VolumeValidator : IRacValidator
{
    public const int WindowCandles = 20;
    public const decimal MinimumRatio = 0.8m;
    public string Id => "volume";
    public int Order => 2;
    public string AgentName => "Bruno";

    public ValidatorResult Validate(ValidationContext c)
    {
        if (c.Candles.Count < WindowCandles + 1)
            return Make(ValidatorVerdict.Unavailable, "Histórico insuficiente para medir volume.", null);
        var last = c.Candles[^1].QuoteVolume;
        var average = c.Candles.Skip(c.Candles.Count - 1 - WindowCandles).Take(WindowCandles)
            .Average(x => x.QuoteVolume);
        if (average <= 0m)
            return Make(ValidatorVerdict.Unavailable, "Sem volume médio de referência.", null);
        var ratio = last / average;
        return ratio >= MinimumRatio
            ? Make(ValidatorVerdict.Approved, $"Volume do candle em {ratio:0.##}x a média de {WindowCandles}.", ratio)
            : Make(ValidatorVerdict.Blocked,
                $"Volume fraco: {ratio:0.##}x a média de {WindowCandles} (mínimo {MinimumRatio}x).", ratio);
    }

    private ValidatorResult Make(ValidatorVerdict v, string reason, decimal? score) =>
        new(Id, Order, AgentName, v, reason, score);
}

public sealed record RacNewsEvent(string Title, DateTimeOffset TimeUtc, string Impact);

/// <summary>Conecte aqui uma fonte real de calendário/notícias. Retornar null = sem fonte.</summary>
public interface IRacNewsCalendar
{
    IReadOnlyList<RacNewsEvent>? EventsAround(string symbol, DateTimeOffset nowUtc);
}

/// <summary>03 Notícias: bloqueia janela de evento de alto impacto. Sem fonte, fica "não verificado".</summary>
public sealed class NewsValidator(IRacNewsCalendar? calendar) : IRacValidator
{
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
    public string Id => "noticias";
    public int Order => 3;
    public string AgentName => "Rebeca";

    public ValidatorResult Validate(ValidationContext c)
    {
        var events = calendar?.EventsAround(c.Symbol, c.NowUtc);
        if (events is null)
            return new(Id, Order, AgentName, ValidatorVerdict.Unavailable,
                "Sem fonte de notícias integrada; não verificado.", null);
        var hit = events.FirstOrDefault(e =>
            string.Equals(e.Impact, "high", StringComparison.OrdinalIgnoreCase) &&
            (e.TimeUtc - c.NowUtc).Duration() <= Window);
        return hit is null
            ? new(Id, Order, AgentName, ValidatorVerdict.Approved,
                $"Nenhum evento de alto impacto em ±{Window.TotalMinutes:0} min.", null)
            : new(Id, Order, AgentName, ValidatorVerdict.Blocked,
                $"Evento de alto impacto na janela: {hit.Title}.", null);
    }
}

/// <summary>04 Risco: travas de perda, limite de posições e tamanho de posição possível.</summary>
public sealed class RiskValidator : IRacValidator
{
    public string Id => "risco";
    public int Order => 4;
    public string AgentName => "Carlos";

    public ValidatorResult Validate(ValidationContext c)
    {
        var lockReason = PaperRisk.LockReason(c.EquityBrl, c.DailyStartEquityBrl, c.PeakEquityBrl);
        if (lockReason is not null) return Make(ValidatorVerdict.Blocked, lockReason);
        if (c.OpenPositions >= PaperRisk.MaxOpenPositions)
            return Make(ValidatorVerdict.Blocked, "Limite de posições abertas atingido.");
        if (c.QuoteUsdt is not > 0m || c.UsdtBrl is not > 0m || c.Signal.Atr14 is not > 0m)
            return new(Id, Order, AgentName, ValidatorVerdict.Unavailable,
                "Sem cotação ou ATR para dimensionar o risco.", null);
        var price = c.QuoteUsdt.Value * c.UsdtBrl.Value * (1m + PaperRisk.SlippagePercentPerSide / 100m);
        var atr = c.Signal.Atr14.Value * c.UsdtBrl.Value;
        var qty = PaperRisk.Quantity(c.CashBrl, c.EquityBrl, price, atr, c.ExposureBrl,
            c.OpenPositions, 0);
        return qty > 0m
            ? Make(ValidatorVerdict.Approved, "Dentro dos limites de risco e exposição.")
            : Make(ValidatorVerdict.Blocked, "Tamanho de posição zero dentro dos limites de risco.");
    }

    private ValidatorResult Make(ValidatorVerdict v, string reason) =>
        new(Id, Order, AgentName, v, reason, null);
}

/// <summary>05 Entrada: sinal de compra vigente e cotação coerente com o candle fechado.</summary>
public sealed class EntryValidator : IRacValidator
{
    public const decimal MaxQuoteDeviation = 0.03m;
    public string Id => "entrada";
    public int Order => 5;
    public string AgentName => "Diego";

    public ValidatorResult Validate(ValidationContext c)
    {
        if (c.Signal.Status != "READY" || c.Signal.Action != "COMPRA_OBSERVACIONAL")
            return Make(ValidatorVerdict.Blocked, "Sem sinal de compra vigente.", null);
        if (c.QuoteUsdt is not > 0m || c.Signal.PriceUsdt is not > 0m)
            return Make(ValidatorVerdict.Blocked, "Sem cotação pública atual para a entrada.", null);
        var ratio = c.QuoteUsdt.Value / c.Signal.PriceUsdt.Value;
        return Math.Abs(ratio - 1m) <= MaxQuoteDeviation
            ? Make(ValidatorVerdict.Approved, "Cotação coerente com o candle fechado.", ratio)
            : Make(ValidatorVerdict.Blocked,
                $"Cotação {((ratio - 1m) * 100m):+0.##;-0.##}% fora do candle fechado (limite ±{MaxQuoteDeviation * 100m:0}%).", ratio);
    }

    private ValidatorResult Make(ValidatorVerdict v, string reason, decimal? score) =>
        new(Id, Order, AgentName, v, reason, score);
}
