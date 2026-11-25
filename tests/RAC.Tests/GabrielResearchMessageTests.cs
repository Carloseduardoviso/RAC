using RAC.Application;
using System.Text.Json;
namespace RAC.Tests;
public sealed class GabrielResearchMessageTests
{
    [Fact]
    public void DynamicReportUsesVersionedIdentityAndValidatedUniverse()
    {
        var symbols=new[]{"ABCUSDT","XYZUSDT"};
        var rows=ResearchExperimentPlan.Build(symbols).Select(p=>new GabrielExperimentMatrix.Assessment(p.Symbol,p.Parameters,
            new(6,-1m,1,2,40,10,0,"EXPLORATORY_ONLY"))).ToArray();
        var task=Guid.NewGuid();var now=DateTimeOffset.UtcNow;
        var report=GabrielResearchMessage.Create(task,now,new string('A',64),rows,symbols);
        var evidence=new RAC.Infrastructure.RebecaMarketEvidence(task,report.Id,"gabriel",now,report.Content);
        Assert.Equal("INDEPENDENT_VALIDATION_REQUIRED",RAC.Infrastructure.RebecaGabrielReportReview.Review(evidence).Status);
        Assert.Equal("INVALID_REPORT",RAC.Infrastructure.RebecaGabrielReportReview.Review(evidence with {Content=report.Content.Replace("ABCUSDT","abcusdt")}).Status);
        Assert.Throws<ArgumentException>(()=>GabrielResearchMessage.Create(task,now,new string('A',64),rows));
        Assert.Throws<ArgumentException>(()=>GabrielResearchMessage.Create(task,now,new string('A',64),rows,new[]{"BTCUSDT","XYZUSDT"}));
    }
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
