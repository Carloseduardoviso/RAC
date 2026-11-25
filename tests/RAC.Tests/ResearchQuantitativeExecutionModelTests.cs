using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class ResearchQuantitativeExecutionModelTests
{
    [Fact]
    public void ModelHasDeterministicPrimaryKeyAndHistoryIndex()
    {
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=RacModelOnly;Integrated Security=true").Options;
        using var db=new RacDbContext(options);
        var entity=db.Model.FindEntityType(typeof(ResearchQuantitativeExecutionRecord))!;
        Assert.Equal("ExecutionId",Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
        Assert.Equal(64,entity.FindProperty("ExecutionId")!.GetMaxLength());
        Assert.Contains(entity.GetIndexes(),i=>i.Properties.Select(p=>p.Name).SequenceEqual(new[]{"ConfigurationId","Month"}));
        Assert.False(entity.FindProperty("ResultJson")!.IsNullable);
    }
}
