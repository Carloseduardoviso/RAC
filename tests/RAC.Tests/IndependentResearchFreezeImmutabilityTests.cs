using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Infrastructure;
namespace RAC.Tests;
public sealed class IndependentResearchFreezeImmutabilityTests
{
    [Fact]
    public async Task TrackedFreezeCannotBeUpdatedOrDeleted()
    {
        var options=new DbContextOptionsBuilder<RacDbContext>().UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=RAC_FREEZE_GUARD_UNUSED;Integrated Security=True").Options;
        await using var db=new RacDbContext(options);
        var row=new IndependentResearchFreezeRecord{FreezeId=new string('A',64)};
        db.Attach(row);
        row.Symbol="CHANGED";
        Assert.Throws<InvalidOperationException>(()=>db.SaveChanges());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
        db.Entry(row).State=EntityState.Unchanged;
        db.Remove(row);
        Assert.Throws<InvalidOperationException>(()=>db.SaveChanges());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
    }
}
