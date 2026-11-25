namespace RAC.Application;
public sealed record MiguelOpenPosition(string Symbol,decimal Quantity,decimal EntryPrice,
    decimal EntryFee,decimal LastPrice);
public sealed record MiguelPositionAudit(decimal? CostBasis,decimal? MarkedValue,
    decimal? Unrealized,IReadOnlyList<string> Findings)
{
    public bool TradingAuthorized=>false;
}
public static class MiguelPositionReconciliation
{
    public static MiguelPositionAudit Evaluate(IReadOnlyList<MiguelFill> fills,
        IReadOnlyList<MiguelOpenPosition> positions,decimal moneyTolerance,decimal quantityTolerance)
    {
        if(moneyTolerance<0 || quantityTolerance<0 || positions.Count>10000)
            throw new ArgumentException("Invalid position audit inputs.");
        var movement=MiguelMovementReconciliation.Evaluate(fills,0,0,moneyTolerance);
        if(movement.ExpectedCash is null)
            return new(null,null,null,["INCOMPLETE_FILL_HISTORY"]);
        var open=new Dictionary<string,MiguelFill>(StringComparer.Ordinal);
        foreach(var f in fills.OrderBy(f=>f.ExecutedUtc).ThenBy(f=>f.Id))
            if(f.Side=="BUY") open.Add(f.Symbol,f); else open.Remove(f.Symbol);
        var findings=new List<string>();
        var seen=new HashSet<string>(StringComparer.Ordinal);
        decimal basis=0,marked=0;
        foreach(var p in positions)
        {
            if(string.IsNullOrWhiteSpace(p.Symbol) || !seen.Add(p.Symbol) ||
                p.Quantity<=0 || p.EntryPrice<=0 || p.EntryFee<0 || p.LastPrice<=0)
            { findings.Add("INVALID_POSITION:"+p.Symbol); continue; }
            basis+=p.Quantity*p.EntryPrice+p.EntryFee;
            marked+=p.Quantity*p.LastPrice;
            if(!open.Remove(p.Symbol,out var f))
            { findings.Add("POSITION_WITHOUT_BUY:"+p.Symbol); continue; }
            if(Math.Abs(p.Quantity-f.Quantity)>quantityTolerance)
                findings.Add("QUANTITY_MISMATCH:"+p.Symbol);
            if(Math.Abs(p.EntryPrice-f.Price)>moneyTolerance)
                findings.Add("ENTRY_PRICE_MISMATCH:"+p.Symbol);
            if(Math.Abs(p.EntryFee-f.Fee)>moneyTolerance)
                findings.Add("ENTRY_FEE_MISMATCH:"+p.Symbol);
        }
        foreach(var symbol in open.Keys.OrderBy(x=>x,StringComparer.Ordinal))
            findings.Add("MISSING_POSITION:"+symbol);
        return findings.Count==0?new(basis,marked,marked-basis,findings.AsReadOnly()):
            new(null,null,null,findings.AsReadOnly());
    }
}
