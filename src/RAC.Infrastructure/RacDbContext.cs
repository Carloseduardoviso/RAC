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
    public DbSet<SimulationCheckpoint> SimulationCheckpoints => Set<SimulationCheckpoint>();
    public DbSet<RacUser> Users => Set<RacUser>();
    public DbSet<MarketQuote> MarketQuotes => Set<MarketQuote>();
    public DbSet<MarketCandle> MarketCandles => Set<MarketCandle>();
    public DbSet<SpotPortfolioSnapshot> SpotPortfolioSnapshots => Set<SpotPortfolioSnapshot>();
    public DbSet<SpotPortfolioHolding> SpotPortfolioHoldings => Set<SpotPortfolioHolding>();
    public DbSet<SpotCashFlowNote> SpotCashFlowNotes => Set<SpotCashFlowNote>();
    public DbSet<SpotInvestmentPlan> SpotInvestmentPlans => Set<SpotInvestmentPlan>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("rac");
        model.Entity<SpotPortfolioSnapshot>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OwnerUserId, x.WalletObservedUtc });
            e.Property(x => x.UsdtBrlRate).HasPrecision(28, 12);
            e.Property(x => x.ValuedSubsetBrl).HasPrecision(28, 12);
            e.Property(x => x.TotalEstimatedBrl).HasPrecision(28, 12);
            e.HasMany(x => x.Holdings).WithOne(x => x.Snapshot)
                .HasForeignKey(x => x.SnapshotId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<SpotPortfolioHolding>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Asset).HasMaxLength(24).IsRequired();
            e.Property(x => x.PriceSource).HasMaxLength(40).IsRequired();
            e.Property(x => x.Free).HasPrecision(28, 12);
            e.Property(x => x.Locked).HasPrecision(28, 12);
            e.Property(x => x.UnitPriceBrl).HasPrecision(28, 12);
            e.Property(x => x.EstimatedValueBrl).HasPrecision(28, 12);
            e.HasIndex(x => new { x.SnapshotId, x.Asset }).IsUnique();
        });
        model.Entity<SpotCashFlowNote>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OwnerUserId, x.OccurredOn });
            e.Property(x => x.Kind).HasMaxLength(20).IsRequired();
            e.Property(x => x.AmountBrl).HasPrecision(19, 4);
            e.Property(x => x.Description).HasMaxLength(240).IsRequired();
        });
        model.Entity<SpotInvestmentPlan>(e =>
        {
            e.HasKey(x => x.OwnerUserId);
            e.Property(x => x.MonthlyContributionBrl).HasPrecision(19, 4);
            e.Property(x => x.ReinvestRealizedProfitPercent).HasPrecision(7, 2);
        });
        model.Entity<MarketQuote>(e =>
        {
            e.HasKey(x => x.Symbol);
            e.Property(x => x.Symbol).HasMaxLength(24).IsRequired();
            e.Property(x => x.Source).HasMaxLength(40).IsRequired();
            e.Property(x => x.PriceUsdt).HasPrecision(28, 12);
            e.HasIndex(x => x.ObservedUtc);
        });
        model.Entity<MarketCandle>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(24).IsRequired();
            e.Property(x => x.Interval).HasMaxLength(5).IsRequired();
            e.Property(x => x.Source).HasMaxLength(40).IsRequired();
            e.Property(x => x.Open).HasPrecision(28, 12);
            e.Property(x => x.High).HasPrecision(28, 12);
            e.Property(x => x.Low).HasPrecision(28, 12);
            e.Property(x => x.Close).HasPrecision(28, 12);
            e.Property(x => x.Volume).HasPrecision(28, 12);
            e.Property(x => x.QuoteVolume).HasPrecision(28, 12);
            e.HasIndex(x => new { x.Symbol, x.Interval, x.OpenUtc }).IsUnique();
            e.HasIndex(x => x.CloseUtc);
        });
        model.Entity<RacUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserName).HasMaxLength(80).IsRequired();
            e.Property(x => x.NormalizedUserName).HasMaxLength(80).IsRequired();
            e.HasIndex(x => x.NormalizedUserName).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            e.Property(x => x.Role).HasMaxLength(30).IsRequired();
        });
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
            e.Property(x => x.SimulationKey).HasMaxLength(100);
            e.HasIndex(x => x.SimulationKey).IsUnique();
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
        model.Entity<SimulationCheckpoint>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.StateJson).HasColumnType("nvarchar(max)").IsRequired();
            e.Property(x => x.RowVersion).IsRowVersion();
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
