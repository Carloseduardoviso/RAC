namespace RAC.Domain;

/// <summary>
/// Veredito da cadeia de validação (Clara) para uma oportunidade de compra em um candle fechado.
/// Só existe para oportunidades (sinal de compra), não para todo candle.
/// </summary>
public sealed class PaperChainDecision
{
    public long Id { get; set; }
    public required string Symbol { get; set; }
    public DateTimeOffset CandleClosedUtc { get; set; }
    public DateTimeOffset RecordedUtc { get; set; }
    public bool Approved { get; set; }
    /// <summary>true se o veredito realmente podia bloquear a compra (Enforce ligado).</summary>
    public bool Enforced { get; set; }
    public string? FirstBlockedBy { get; set; }
    public string? FirstBlockReason { get; set; }
    public required string ResultsJson { get; set; }
}
