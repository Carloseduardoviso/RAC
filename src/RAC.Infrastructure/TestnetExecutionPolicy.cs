using RAC.Domain;
using RAC.Application;

namespace RAC.Infrastructure;

/// <summary>
/// Deterministic safety decisions shared by the Testnet executor and isolated tests.
/// Unknown remote outcomes are never permission to place a replacement order.
/// </summary>
public static class TestnetExecutionPolicy
{
    public const string TestnetHost = "testnet.binance.vision";
    // MVP 1.2.1 explicitly forbids any live Testnet POST/DELETE.
    // A separately reviewed release must change this source-code gate.
    // Environment variables, credentials, and UI cannot override it.
    public static bool SubmissionExplicitlyAllowed => false;

    public static string ClientOrderId(long paperFillId)
    {
        if (paperFillId <= 0 || paperFillId > 99999999999999L)
            throw new ArgumentOutOfRangeException(nameof(paperFillId));
        return "RAC12" + paperFillId.ToString("D14", System.Globalization.CultureInfo.InvariantCulture);
    }

    public static bool IsUnsettled(string? status) =>
        status is "SUBMITTING" or "UNKNOWN" or "NEW" or "PARTIALLY_FILLED";

    public static bool IsTerminal(string? status) =>
        status is "FILLED" or "CANCELED" or "EXPIRED" or "REJECTED" or "EXPIRED_IN_MATCH";

    /// <summary>Only an explicitly enabled, healthy, current PAPER account can authorize test orders.</summary>
    public static string? BlockReason(TestnetExecutionState state, PaperAccount? paper,
        DateTimeOffset now, bool hasUnsettled)
    {
        if (!SubmissionExplicitlyAllowed)
            return "Envio Testnet desligado por padrão; homologação com ordens não autorizada.";
        if (!state.Enabled || state.EmergencyStopped || state.CircuitLocked)
            return "Executor Testnet pausado ou bloqueado.";
        var risk=PaperSafetyReason(paper,now);
        if (risk is not null)return risk;
        if (hasUnsettled)
            return "Ordem Testnet pendente ou ambígua; conciliação obrigatória.";
        return null;
    }

    public static string? PaperSafetyReason(PaperAccount? paper,DateTimeOffset now)
    {
        if(paper is null || !paper.Running || paper.RiskLocked)
            return "Carteira PAPER inativa ou bloqueada.";
        if(paper.LastCycleUtc is null || now-paper.LastCycleUtc>TimeSpan.FromMinutes(4) ||
            paper.LastCycleUtc>now.AddMinutes(1))
            return "Ciclo PAPER desatualizado.";
        if(PaperRisk.LockReason(paper.LastEquityBrl,paper.DailyStartEquityBrl,
            paper.PeakEquityBrl) is not null)
            return "Limites PAPER de perda ou drawdown atingidos.";
        return null;
    }

    public static bool HasConfirmedSellQuantity(
        IEnumerable<TestnetOrderRecord> confirmed, string symbol, decimal quantity)
    {
        if(quantity<=0 || !RacMarketSymbols.IsSupported(symbol))return false;
        decimal net=0;
        foreach(var order in confirmed)
        {
            if(order.Symbol!=symbol || order.Status!="FILLED")continue;
            if(order.Side=="BUY")net+=order.ExecutedQuantity;
            else if(order.Side=="SELL")net-=order.ExecutedQuantity;
        }
        return net>=quantity;
    }

    public static void Reconcile(TestnetOrderRecord intent, TestnetExchangeOrder response,
        DateTimeOffset now)
    {
        if(response.ClientOrderId!=intent.ClientOrderId || response.Symbol!=intent.Symbol ||
           !RacMarketSymbols.IsSupported(response.Symbol) ||
           !new[]{"NEW","PARTIALLY_FILLED","FILLED","CANCELED","REJECTED","EXPIRED","EXPIRED_IN_MATCH"}
               .Contains(response.Status, StringComparer.Ordinal) ||
           response.ExecutedQuantity<0 || response.ExecutedQuantity>intent.Quantity ||
           response.QuoteQuantity<0 || string.IsNullOrWhiteSpace(response.OrderId))
            throw new InvalidOperationException("Resposta da Testnet incompatível com intenção SQL.");
        if(intent.Status=="FILLED" && response.Status!="FILLED")
            throw new InvalidOperationException("Ordem FILLED não pode regredir.");
        if(response.ExecutedQuantity<intent.ExecutedQuantity)
            throw new InvalidOperationException("Quantidade executada regrediu.");
        intent.ExchangeOrderId=response.OrderId;
        intent.Status=response.Status;
        intent.ExecutedQuantity=response.ExecutedQuantity;
        intent.CumulativeQuoteQuantity=response.QuoteQuantity;
        intent.UpdatedUtc=now;
        intent.Note="";
    }
}
