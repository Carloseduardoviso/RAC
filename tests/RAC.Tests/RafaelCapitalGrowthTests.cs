using RAC.Application;
namespace RAC.Tests;
public sealed class RafaelCapitalGrowthTests
{
    [Fact]
    public void SeparatesContributionsAndProfitAndRejectsUnreconciledReinvestment()
    {
        var snapshot=new RafaelGrowthSnapshot(90,30,25,100,15,false);
        var result=RafaelCapitalGrowth.Evaluate(snapshot,50);
        Assert.Equal(120m,result.Equity);
        Assert.Equal(5m,result.UnrealizedProfit);
        Assert.Equal(20m,result.TotalNetProfit);
        Assert.Equal(0m,result.ReconciliationDifference);
        Assert.Equal(20m,result.NetProfitToContributionsPercent);
        Assert.Equal(7.5m,result.ResearchReinvestmentCeiling);
        Assert.False(result.TradingAuthorized);
        Assert.Equal(0m,RafaelCapitalGrowth.Evaluate(snapshot with {RiskLocked=true},100).ResearchReinvestmentCeiling);
        Assert.Equal("RECONCILIATION_REQUIRED",RafaelCapitalGrowth.Evaluate(snapshot with {RealizedNetProfit=14},100).Status);
        Assert.Equal(0m,RafaelCapitalGrowth.Evaluate(snapshot with {RealizedNetProfit=14},100).ResearchReinvestmentCeiling);
        var deposit=RafaelCapitalGrowth.Evaluate(snapshot with {Cash=190,NetContributions=200},50);
        Assert.Equal(result.TotalNetProfit,deposit.TotalNetProfit);
        Assert.Equal(result.ResearchReinvestmentCeiling,deposit.ResearchReinvestmentCeiling);
        Assert.Throws<ArgumentException>(()=>RafaelCapitalGrowth.Evaluate(snapshot,101));
    }
}
