namespace RAC.Application;
public sealed record MiguelFill(long Id,string Symbol,string Side,decimal Quantity,decimal Price,
    decimal Fee,decimal? Realized,DateTimeOffset ExecutedUtc);
public sealed record MiguelMovementAudit(decimal? ExpectedCash,decimal? CashDifference,
    decimal? CalculatedRealized,decimal TotalFees,IReadOnlyList<string> Findings)
{
    public bool TradingAuthorized=>false;
}
public static class MiguelMovementReconciliation
{
    // PAPER currently opens one position per symbol and closes it in full.
    public static MiguelMovementAudit Evaluate(IReadOnlyList<MiguelFill> fills,
        decimal contributions,decimal cash,decimal tolerance)
    {
        if(tolerance<0 || fills.Count>100000) throw new ArgumentException("Invalid audit inputs.");
        var findings=new List<string>();
        var open=new Dictionary<string,MiguelFill>(StringComparer.Ordinal);
        var ids=new HashSet<long>();
        decimal expected=contributions,realized=0,fees=0;
        bool complete=true;
        foreach(var f in fills.OrderBy(f=>f.ExecutedUtc).ThenBy(f=>f.Id))
        {
            if(f.Id<=0 || !ids.Add(f.Id) || string.IsNullOrWhiteSpace(f.Symbol) ||
                f.Quantity<=0 || f.Price<=0 || f.Fee<0 || f.Fee>f.Quantity*f.Price ||
                f.Side is not ("BUY" or "SELL"))
            { findings.Add("INVALID_FILL:"+f.Id); complete=false; continue; }
            var gross=f.Quantity*f.Price;
            fees+=f.Fee;
            if(f.Side=="BUY")
            {
                expected-=gross+f.Fee;
                if(f.Realized.HasValue) findings.Add("BUY_REALIZED:"+f.Id);
                if(!open.TryAdd(f.Symbol,f))
                { findings.Add("OVERLAPPING_BUY:"+f.Id); complete=false; }
            }
            else
            {
                expected+=gross-f.Fee;
                if(!open.Remove(f.Symbol,out var entry) || entry.Quantity!=f.Quantity)
                { findings.Add("UNMATCHED_SELL:"+f.Id); complete=false; continue; }
                var pnl=gross-f.Fee-entry.Quantity*entry.Price-entry.Fee;
                realized+=pnl;
                if(!f.Realized.HasValue || Math.Abs(f.Realized.Value-pnl)>tolerance)
                    findings.Add("REALIZED_MISMATCH:"+f.Id);
            }
        }
        if(complete && Math.Abs(expected-cash)>tolerance) findings.Add("CASH_MISMATCH");
        return new(complete?expected:null,complete?expected-cash:null,complete?realized:null,fees,findings.AsReadOnly());
    }
}
