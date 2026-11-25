using RAC.Application;
using System.Globalization;
namespace RAC.Tests;

public sealed class ResearchExperimentIdentityTests
{
    [Fact]
    public void AllPlannedConfigurationsHaveUniqueStableIdentities()
    {
        var plan=ResearchExperimentPlan.Build();
        var ids=plan.Select(ResearchExperimentIdentity.Create).ToArray();
        Assert.Equal(288,ids.Distinct().Count());
        Assert.All(ids,x=>Assert.Equal(64,x.Length));
        Assert.Equal(ids,ResearchExperimentPlan.Build().Select(ResearchExperimentIdentity.Create));
    }

    [Fact]
    public void IdentityIgnoresDecimalScaleAndCulture()
    {
        var row=ResearchExperimentPlan.Build(["ABCUSDT"])[0];
        var expected=ResearchExperimentIdentity.Create(row);
        var previous=CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("pt-BR");
            Assert.Equal(expected,ResearchExperimentIdentity.Create(row with {Parameters=row.Parameters with {FeePctPerSide=0.1000m}}));
        }
        finally {CultureInfo.CurrentCulture=previous;}
    }

    [Fact]
    public void RejectsUnauthorizedOrUnplannedConfigurations()
    {
        var row=ResearchExperimentPlan.Build(["ABCUSDT"])[0];
        Assert.Throws<ArgumentException>(()=>ResearchExperimentIdentity.Create(row with {Parameters=row.Parameters with {TradingAuthorized=true}}));
        Assert.Throws<ArgumentException>(()=>ResearchExperimentIdentity.Create(row with {Parameters=row.Parameters with {ExposurePct=99}}));
    }

    [Fact]
    public void DynamicSymbolsAndCostScenariosHaveDistinctIdentities()
    {
        var plan=ResearchExperimentPlan.Build(["ABCUSDT","XYZUSDT"]);
        Assert.Equal(48,plan.Select(ResearchExperimentIdentity.Create).Distinct().Count());
    }
}
