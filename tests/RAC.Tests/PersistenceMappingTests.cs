using Microsoft.EntityFrameworkCore;
using RAC.Domain;
using RAC.Infrastructure;

namespace RAC.Tests;

public sealed class PersistenceMappingTests
{
    private static RacDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<RacDbContext>()
            .UseSqlServer("Server=(local);Database=ModelInspectionOnly;Integrated Security=True;TrustServerCertificate=True")
            .Options);

    [Fact]
    public void Orders_have_unique_client_order_id()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(TradeOrder));
        Assert.NotNull(entity);
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Count == 1 &&
            index.Properties[0].Name == nameof(TradeOrder.ClientOrderId));
    }

    [Fact]
    public void Models_include_risk_lock_and_audit()
    {
        using var db = CreateContext();
        Assert.NotNull(db.Model.FindEntityType(typeof(RiskLock)));
        Assert.NotNull(db.Model.FindEntityType(typeof(AuditEvent)));
        Assert.Equal("rac", db.Model.GetDefaultSchema());
    }

    [Fact]
    public void New_order_is_simulated_by_default()
    {
        var order = new TradeOrder { ClientOrderId = Guid.NewGuid().ToString("N"), Symbol = "BTCUSDT", Side = "BUY" };
        Assert.Equal(OperationMode.Simulation, order.Mode);
        Assert.Equal(OrderStatus.Proposed, order.Status);
    }
}
