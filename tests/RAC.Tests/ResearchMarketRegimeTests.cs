using RAC.Application;
public class ResearchMarketRegimeTests
{
    [Fact] public void FlatMarketIsNeutral()
    {
        var r=ResearchMarketRegime.Evaluate(Enumerable.Repeat(new ResearchMarketRegime.Bar(100,100,100),40).ToArray());
        Assert.Equal("SIDEWAYS",r.Regime); Assert.Equal(50m,r.Rsi); Assert.Equal(0m,r.Atr);
    }
    [Theory]
    [InlineData(1,"UP",100)]
    [InlineData(-1,"DOWN",0)]
    public void DirectionalMarket(int direction,string expected,int rsi)
    {
        var r=ResearchMarketRegime.Evaluate(Enumerable.Range(0,40).Select(i=>new ResearchMarketRegime.Bar(201+i*direction,199+i*direction,200+i*direction)).ToArray());
        Assert.Equal(expected,r.Regime); Assert.Equal((decimal)rsi,r.Rsi); Assert.True(r.Atr>0);
    }
    [Fact] public void RejectsInsufficientHistory()=>Assert.Throws<ArgumentException>(()=>ResearchMarketRegime.Evaluate(Array.Empty<ResearchMarketRegime.Bar>()));
}
