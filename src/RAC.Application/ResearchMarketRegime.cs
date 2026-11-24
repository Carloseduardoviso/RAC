namespace RAC.Application;

public static class ResearchMarketRegime
{
    public record Bar(decimal High, decimal Low, decimal Close);
    public record Reading(string Regime, decimal Rsi, decimal Macd, decimal Ema, decimal Atr);
    public static Reading Evaluate(IReadOnlyList<Bar> bars)
    {
        if (bars.Count < 35 || bars.Any(b => b.Low <= 0 || b.High < b.Close || b.Low > b.Close))
            throw new ArgumentException("Invalid or insufficient closed bars");
        decimal fast=bars[0].Close, slow=fast, signal=0, gain=0, loss=0, atr=0;
        for(int i=1;i<bars.Count;i++)
        {
            var b=bars[i]; var prev=bars[i-1].Close; var delta=b.Close-prev;
            fast+=(b.Close-fast)*2/13; slow+=(b.Close-slow)*2/27;
            signal+=(fast-slow-signal)*2/10;
            var tr=Math.Max(b.High-b.Low,Math.Max(Math.Abs(b.High-prev),Math.Abs(b.Low-prev)));
            if(i<=14){gain+=Math.Max(delta,0)/14;loss+=Math.Max(-delta,0)/14;atr+=tr/14;}
            else {gain=(gain*13+Math.Max(delta,0))/14;loss=(loss*13+Math.Max(-delta,0))/14;atr=(atr*13+tr)/14;}
        }
        var rsi=loss==0 ? (gain==0 ? 50 : 100) : 100-100/(1+gain/loss);
        var histogram=fast-slow-signal;
        var regime=fast>slow*1.002m && histogram>0 ? "UP" : fast<slow*.998m && histogram<0 ? "DOWN" : "SIDEWAYS";
        return new(regime,rsi,histogram,slow,atr);
    }
}
