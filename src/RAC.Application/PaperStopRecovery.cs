namespace RAC.Application;

// Pure recovery preview: no persistence, execution or authorization capability.
public static class PaperStopRecovery
{
    public record Position(long Id, string Symbol, decimal Quantity, decimal EntryPrice, decimal EntryFee, decimal StopPrice);
    public record Preview(long PositionId, string Symbol, decimal Quantity, decimal SellPrice, decimal Fee, decimal Received, decimal Realized, decimal EquityAfter, DateTimeOffset QuoteUtc);
    public static Preview Calculate(bool running, bool locked, Position position, decimal cash, decimal dotUsdt, decimal usdtBrl, DateTimeOffset quoteUtc, DateTimeOffset now)
    {
        if(running || !locked || position.Id<=0 || string.IsNullOrWhiteSpace(position.Symbol) ||
           position.Quantity<=0 || position.EntryPrice<=0 || position.EntryFee<0 || position.StopPrice<=0 ||
           cash<0 || dotUsdt<=0 || usdtBrl<=0 || quoteUtc>now || now-quoteUtc>TimeSpan.FromSeconds(30))
            throw new InvalidOperationException("Invalid or stale recovery evidence.");
        var mark=dotUsdt*usdtBrl;
        if(mark>position.StopPrice) throw new InvalidOperationException("Stop not breached.");
        var sell=mark*(1-PaperRisk.SlippagePercentPerSide/100);
        var gross=position.Quantity*sell;
        var fee=gross*PaperRisk.FeePercentPerSide/100;
        var received=gross-fee;
        return new(position.Id,position.Symbol,position.Quantity,sell,fee,received,
            received-position.Quantity*position.EntryPrice-position.EntryFee,cash+received,quoteUtc);
    }
}
