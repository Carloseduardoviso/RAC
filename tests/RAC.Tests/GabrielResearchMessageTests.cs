using RAC.Application;
using System.Text.Json;
namespace RAC.Tests;
public sealed class GabrielResearchMessageTests
{
    [Fact]
    public void CreatesStableResearchOnlyReport()
    {
        var rows=ResearchExperimentPlan.Build().Select(p=>new GabrielExperimentMatrix.Assessment(p.Symbol,p.Parameters,
            new(6,-1m,1,2,40,10,0,"EXPLORATORY_ONLY"))).ToArray();
        var id=Guid.NewGuid();var now=DateTimeOffset.UtcNow;
        var a=GabrielResearchMessage.Create(id,now,new string('A',64),rows);
        Assert.Equal(a,GabrielResearchMessage.Create(id,now,new string('A',64),rows));
        Assert.Equal("rebeca",a.Recipient);
        Assert.Equal("gabriel",a.Sender);
        using var doc=JsonDocument.Parse(a.Content);
        Assert.False(doc.RootElement.GetProperty("TradingAuthorized").GetBoolean());
        Assert.Equal(0,doc.RootElement.GetProperty("FurtherResearch").GetInt32());
        Assert.Throws<ArgumentException>(()=>GabrielResearchMessage.Create(id,now,"invalid",rows));
    }
}
