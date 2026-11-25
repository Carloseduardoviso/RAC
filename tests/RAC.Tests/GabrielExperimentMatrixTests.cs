using RAC.Application;
using System.Text.Json;
namespace RAC.Tests;
public sealed class GabrielExperimentMatrixTests
{
    [Fact]
    public void SeparatesExactParametersAndRejectsIncompleteMatrix()
    {
        var months=new[]{"2026-05","2026-06","2026-07","2026-08","2026-09","2026-10"};
        var rows=ResearchExperimentPlan.Build().SelectMany(p=>months.Select(m=>
            new GabrielExperimentMatrix.Row(m,"EXPLORATORY",p.Symbol,p.Parameters,new(1m,2m,5,3,2m,1m,false)))).ToArray();
        string Encode(IEnumerable<GabrielExperimentMatrix.Row> r)=>JsonSerializer.Serialize(new {Mode="RESEARCH_ONLY",TradingAuthorized=false,IndependentValidation=false,Rows=r});
        var result=GabrielExperimentMatrix.Read(Encode(rows));
        Assert.Equal(288,result.Count);
        Assert.All(result,x=>{Assert.Equal(6,x.Evidence.Months);Assert.False(x.Evidence.TradingAuthorized);});
        Assert.Throws<InvalidDataException>(()=>GabrielExperimentMatrix.Read(Encode(rows.Skip(1))));
    }
}
