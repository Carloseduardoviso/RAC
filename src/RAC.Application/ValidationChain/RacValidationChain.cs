using RAC.Domain;

namespace RAC.Application;

public enum ValidatorVerdict { Approved, Blocked, Unavailable }

/// <summary>Veredito de um validador para uma oportunidade de compra.</summary>
public sealed record ValidatorResult(string ValidatorId, int Order, string AgentName,
    ValidatorVerdict Verdict, string Reason, decimal? Score);

/// <summary>Tudo que os validadores podem ler. Nada aqui executa ordens.</summary>
public sealed record ValidationContext(
    string Symbol,
    IReadOnlyList<MarketCandlePoint> Candles, // candles fechados e contíguos, do mais antigo ao mais novo
    StrategySignal Signal,
    DateTimeOffset NowUtc,
    decimal? QuoteUsdt,
    decimal? UsdtBrl,
    decimal CashBrl,
    decimal EquityBrl,
    decimal DailyStartEquityBrl,
    decimal PeakEquityBrl,
    decimal ExposureBrl,
    int OpenPositions);

public interface IRacValidator
{
    string Id { get; }
    int Order { get; }
    string AgentName { get; }
    ValidatorResult Validate(ValidationContext context);
}

public sealed record RacValidationOptions
{
    /// <summary>
    /// false (padrão) = modo sombra: a cadeia roda e grava os vereditos, mas NÃO muda as entradas.
    /// true = uma oportunidade bloqueada pela cadeia não vira compra.
    /// </summary>
    public bool Enforce { get; init; }

    /// <summary>
    /// Validador sem fonte de dados (ex.: notícias) é mostrado como "não verificado", nunca como aprovado.
    /// Com true, ele também bloqueia a oportunidade.
    /// </summary>
    public bool TreatUnavailableAsBlocked { get; init; }
}

public sealed record ChainResult(string Symbol, bool Approved, string? FirstBlockedBy,
    string? FirstBlockReason, IReadOnlyList<ValidatorResult> Results,
    IReadOnlyList<string> Unverified, DateTimeOffset EvaluatedUtc);

/// <summary>
/// Clara: roda os validadores em ordem e só aprova se nenhum bloquear.
/// Falha fechada: exceção em um validador conta como bloqueio.
/// </summary>
public sealed class RacValidationChain
{
    public const string AgentName = "Clara";
    private readonly IRacValidator[] _validators;

    public RacValidationChain(IEnumerable<IRacValidator> validators, RacValidationOptions? options = null)
    {
        _validators = validators.OrderBy(x => x.Order).ToArray();
        Options = options ?? new RacValidationOptions();
    }

    public RacValidationOptions Options { get; }

    public static RacValidationChain CreateDefault(IRacNewsCalendar? news = null,
        RacValidationOptions? options = null) =>
        new([new TrendValidator(), new VolumeValidator(), new NewsValidator(news),
            new RiskValidator(), new EntryValidator()], options);

    public ChainResult Run(ValidationContext context)
    {
        var results = new List<ValidatorResult>(_validators.Length);
        foreach (var validator in _validators)
        {
            try { results.Add(validator.Validate(context)); }
            catch (Exception)
            {
                results.Add(new ValidatorResult(validator.Id, validator.Order, validator.AgentName,
                    ValidatorVerdict.Blocked, "Falha interna do validador; bloqueado por segurança.", null));
            }
        }
        bool IsBlock(ValidatorResult r) => r.Verdict == ValidatorVerdict.Blocked ||
            (r.Verdict == ValidatorVerdict.Unavailable && Options.TreatUnavailableAsBlocked);
        var firstBlock = results.FirstOrDefault(IsBlock);
        var unverified = results.Where(r => r.Verdict == ValidatorVerdict.Unavailable)
            .Select(r => r.ValidatorId).ToArray();
        return new ChainResult(context.Symbol, firstBlock is null, firstBlock?.ValidatorId,
            firstBlock?.Reason, results, unverified, context.NowUtc);
    }
}
