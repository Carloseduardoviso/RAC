using Microsoft.EntityFrameworkCore;
using RAC.Domain;

namespace RAC.Infrastructure;

public sealed class RacDbContext(DbContextOptions<RacDbContext> options) : DbContext(options)
{
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<Strategy> Strategies => Set<Strategy>();
    public DbSet<TradeOrder> Orders => Set<TradeOrder>();
    public DbSet<TradeExecution> Executions => Set<TradeExecution>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Contribution> Contributions => Set<Contribution>();
    public DbSet<EquitySnapshot> EquitySnapshots => Set<EquitySnapshot>();
    public DbSet<RiskLock> RiskLocks => Set<RiskLock>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("rac");
        model.Entity<Agent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.Role).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
        });
        model.Entity<Strategy>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Version).HasMaxLength(40).IsRequired();
            e.HasIndex(x => new { x.Name, x.Version }).IsUnique();
        });
        model.Entity<TradeOrder>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ClientOrderId).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.ClientOrderId).IsUnique();
            e.Property(x => x.Symbol).HasMaxLength(24).IsRequired();
            e.Property(x => x.Side).HasMaxLength(8).IsRequired();
            e.Property(x => x.Quantity).HasPrecision(28, 12);
            e.Property(x => x.Price).HasPrecision(28, 12);
            e.HasIndex(x => x.CreatedUtc);
        });
        model.Entity<TradeExecution>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Quantity).HasPrecision(28, 12);
            e.Property(x => x.Price).HasPrecision(28, 12);
            e.Property(x => x.Fee).HasPrecision(28, 12);
        });
        model.Entity<Position>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(24).IsRequired();
            e.Property(x => x.Quantity).HasPrecision(28, 12);
            e.Property(x => x.AverageEntry).HasPrecision(28, 12);
            e.Property(x => x.StopPrice).HasPrecision(28, 12);
            e.HasIndex(x => new { x.Symbol, x.IsOpen });
        });
        model.Entity<Contribution>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.AmountBrl).HasPrecision(19, 4);
        });
        model.Entity<EquitySnapshot>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EquityBrl).HasPrecision(19, 4);
            e.Property(x => x.TotalContributionsBrl).HasPrecision(19, 4);
            e.Property(x => x.DrawdownPercent).HasPrecision(9, 4);
            e.HasIndex(x => x.RecordedUtc);
        });
        model.Entity<RiskLock>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            e.HasIndex(x => x.Active);
        });
        model.Entity<AuditEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000).IsRequired();
            e.HasIndex(x => x.CreatedUtc);
        });
    }
}
