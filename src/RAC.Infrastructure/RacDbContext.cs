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
    public DbSet<SpotActivity> SpotActivities => Set<SpotActivity>();

    public DbSet<PaperAccount> PaperAccounts => Set<PaperAccount>();
    public DbSet<PaperPosition> PaperPositions => Set<PaperPosition>();
    public DbSet<PaperFill> PaperFills => Set<PaperFill>();
    public DbSet<PaperAgentCursor> PaperAgentCursors => Set<PaperAgentCursor>();

    public DbSet<PaperMonitorSample> PaperMonitorSamples => Set<PaperMonitorSample>();
    public DbSet<PaperDecisionAudit> PaperDecisionAudits => Set<PaperDecisionAudit>();
    public DbSet<PaperChainDecision> PaperChainDecisions => Set<PaperChainDecision>();
    public DbSet<PaperMonitorAlert> PaperMonitorAlerts => Set<PaperMonitorAlert>();

    public DbSet<TestnetOrderRecord> TestnetOrders => Set<TestnetOrderRecord>();
    public DbSet<TestnetExecutionState> TestnetExecutionStates => Set<TestnetExecutionState>();
    public DbSet<ProductionRiskPolicyRecord> ProductionRiskPolicies => Set<ProductionRiskPolicyRecord>();
    public DbSet<ProductionRiskPolicyAudit> ProductionRiskPolicyAudits => Set<ProductionRiskPolicyAudit>();

    public DbSet<ResearchSourceRecord> ResearchSources => Set<ResearchSourceRecord>();
    public DbSet<ResearchPublicationRecord> ResearchPublications => Set<ResearchPublicationRecord>();
    public DbSet<ResearchExecutionRecord> ResearchExecutionHistory => Set<ResearchExecutionRecord>();
    public DbSet<ResearchSourceReviewRecord> ResearchSourceReviews => Set<ResearchSourceReviewRecord>();

    public DbSet<ResearchKnowledgeRecord> ResearchKnowledge => Set<ResearchKnowledgeRecord>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasDefaultSchema("rac");
        model.Entity<ResearchKnowledgeRecord>(e=>{
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Doi).HasMaxLength(255).IsRequired();
            e.Property(x=>x.Category).HasMaxLength(30).IsRequired();
            e.Property(x=>x.Term).HasMaxLength(100).IsRequired();
            e.Property(x=>x.Evidence).HasMaxLength(1000).IsRequired();
            e.Property(x=>x.EvidenceLevel).HasMaxLength(30).IsRequired();
            e.Property(x=>x.ExtractorVersion).HasMaxLength(16).IsRequired();
            e.HasIndex(x=>new{x.Doi,x.Category,x.Term,x.ExtractorVersion}).IsUnique();
            e.HasOne<ResearchPublicationRecord>().WithMany().HasForeignKey(x=>x.Doi).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ResearchExecutionRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Topic).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasMaxLength(30).IsRequired();
            e.Property(x => x.ErrorCategory).HasMaxLength(100);
            e.HasIndex(x => new { x.Topic, x.StartedUtc });
        });
        model.Entity<ResearchPublicationRecord>(e =>
        {
            e.HasKey(x => x.Doi);
            e.Property(x => x.Doi).HasMaxLength(255);
            e.Property(x => x.Title).HasMaxLength(1000).IsRequired();
            e.Property(x => x.AuthorsJson).IsRequired();
        });
        model.Entity<ResearchSourceReviewRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Host).HasMaxLength(253).IsRequired();
            e.Property(x => x.EvidenceJson).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Status).HasMaxLength(40).IsRequired();
            e.Property(x => x.RubricVersion).HasMaxLength(16).IsRequired();
            e.HasIndex(x => new { x.Host, x.ReviewedUtc });
            e.HasOne<ResearchSourceRecord>().WithMany().HasForeignKey(x => x.Host).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<ResearchSourceRecord>(e =>
        {
            e.HasKey(x => x.Host);
            e.Property(x => x.Host).HasMaxLength(253);
            e.Property(x => x.EvidenceUrl).IsRequired().HasMaxLength(2048);
            e.Property(x => x.DiscoveredFrom).IsRequired().HasMaxLength(100);
        });
        model.Entity<ProductionRiskPolicyRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Version).IsUnique();
            e.Property(x => x.LimitsJson).IsRequired().HasMaxLength(2000);
            e.Property(x => x.LimitsSha256).IsRequired().HasMaxLength(64);
            e.Property(x => x.Status).IsRequired().HasMaxLength(32);
            e.Property(x => x.ProposedBy).IsRequired().HasMaxLength(128);
            e.Property(x => x.ReviewedBy).HasMaxLength(128);
            e.Property(x => x.ReviewReason).HasMaxLength(500);
            e.Property(x => x.RowVersion).IsRowVersion();
        });
        model.Entity<ProductionRiskPolicyAudit>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Action).IsRequired().HasMaxLength(40);
            e.Property(x => x.Actor).IsRequired().HasMaxLength(128);
            e.Property(x => x.Reason).IsRequired().HasMaxLength(500);
            e.Property(x => x.LimitsSha256).IsRequired().HasMaxLength(64);
            e.HasIndex(x => new { x.PolicyId, x.CreatedUtc });
        });
        model.Entity<TestnetExecutionState>(e =>
        {
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Id).ValueGeneratedNever();
            e.Property(x=>x.RowVersion).IsRowVersion();
            e.Property(x=>x.LockReason).IsRequired().HasMaxLength(500);
        });
        model.Entity<TestnetOrderRecord>(e =>
        {
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Symbol).IsRequired().HasMaxLength(24);
            e.Property(x=>x.Side).IsRequired().HasMaxLength(8);
            e.Property(x=>x.ClientOrderId).IsRequired().HasMaxLength(36);
            e.Property(x=>x.Status).IsRequired().HasMaxLength(32);
            e.Property(x=>x.Note).IsRequired().HasMaxLength(500);
            e.Property(x=>x.ExchangeOrderId).HasMaxLength(40);
            e.Property(x=>x.Quantity).HasPrecision(28,12);
            e.Property(x=>x.ExecutedQuantity).HasPrecision(28,12);
            e.Property(x=>x.CumulativeQuoteQuantity).HasPrecision(28,12);
            e.Property(x=>x.RowVersion).IsRowVersion();
            e.HasIndex(x=>x.PaperFillId).IsUnique();
            e.HasIndex(x=>x.ClientOrderId).IsUnique();
        });
        model.Entity<PaperMonitorSample>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.RecordedUtc);
            foreach (var property in new[] { nameof(PaperMonitorSample.EquityBrl),
                nameof(PaperMonitorSample.CashBrl), nameof(PaperMonitorSample.ExposureBrl),
                nameof(PaperMonitorSample.TotalContributionsBrl),
                nameof(PaperMonitorSample.DrawdownPercent) })
                e.Property<decimal>(property).HasPrecision(28, 12);
        });
        model.Entity<PaperDecisionAudit>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(24).IsRequired();
            e.Property(x => x.Signal).HasMaxLength(40).IsRequired();
            e.Property(x => x.Outcome).HasMaxLength(40).IsRequired();
            e.Property(x => x.Actor).HasMaxLength(24).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(250).IsRequired();
            e.HasIndex(x => new { x.Symbol, x.CandleClosedUtc }).IsUnique();
            e.HasIndex(x => x.RecordedUtc);
        });
        model.Entity<PaperChainDecision>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(24).IsRequired();
            e.Property(x => x.FirstBlockedBy).HasMaxLength(40);
            e.Property(x => x.FirstBlockReason).HasMaxLength(250);
            e.Property(x => x.ResultsJson).IsRequired();
            e.HasIndex(x => new { x.Symbol, x.CandleClosedUtc }).IsUnique();
            e.HasIndex(x => x.RecordedUtc);
        });
        model.Entity<PaperMonitorAlert>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(80).IsRequired();
            e.Property(x => x.Severity).HasMaxLength(20).IsRequired();
            e.Property(x => x.Message).HasMaxLength(350).IsRequired();
            e.HasIndex(x => new { x.Code, x.ResolvedUtc });
            e.HasIndex(x => x.OpenedUtc);
        });
        model.Entity<PaperAccount>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.RowVersion).IsRowVersion();
            e.Property(x => x.RiskReason).HasMaxLength(500).IsRequired();
            foreach (var property in new[] { nameof(PaperAccount.CashBrl),
                nameof(PaperAccount.TotalContributionsBrl), nameof(PaperAccount.PeakEquityBrl),
                nameof(PaperAccount.DailyStartEquityBrl), nameof(PaperAccount.LastEquityBrl) })
                e.Property<decimal>(property).HasPrecision(28, 12);
        });
        model.Entity<PaperPosition>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(24).IsRequired();
            e.HasIndex(x => x.Symbol).IsUnique();
            foreach (var property in new[] { nameof(PaperPosition.Quantity),
                nameof(PaperPosition.EntryPriceBrl), nameof(PaperPosition.StopPriceBrl),
                nameof(PaperPosition.EntryFeeBrl), nameof(PaperPosition.LastPriceBrl) })
                e.Property<decimal>(property).HasPrecision(28, 12);
        });
        model.Entity<PaperFill>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Symbol).HasMaxLength(24).IsRequired();
            e.Property(x => x.Side).HasMaxLength(8).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(40).IsRequired();
            e.HasIndex(x => x.ExecutedUtc);
            e.HasIndex(x => new { x.Symbol, x.Side, x.SignalClosedUtc }).IsUnique();
            foreach (var property in new[] { nameof(PaperFill.Quantity),
                nameof(PaperFill.PriceBrl), nameof(PaperFill.FeeBrl) })
                e.Property<decimal>(property).HasPrecision(28, 12);
            e.Property(x => x.RealizedPnlBrl).HasPrecision(28, 12);
        });
        model.Entity<PaperAgentCursor>(e =>
        {
            e.HasKey(x => x.Symbol);
            e.Property(x => x.Symbol).HasMaxLength(24).IsRequired();
            e.Property(x => x.LastSignal).HasMaxLength(40).IsRequired();
            e.Property(x => x.Status).HasMaxLength(24).IsRequired();
            e.Property(x => x.Note).HasMaxLength(250).IsRequired();
        });
        model.Entity<SpotActivity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Kind).HasMaxLength(12).IsRequired();
            e.Property(x => x.ReferenceId).HasMaxLength(128).IsRequired();
            e.Property(x => x.Symbol).HasMaxLength(24).IsRequired();
            e.Property(x => x.Asset).HasMaxLength(24).IsRequired();
            e.Property(x => x.Side).HasMaxLength(8).IsRequired();
            e.Property(x => x.Status).HasMaxLength(40).IsRequired();
            e.Property(x => x.FeeAsset).HasMaxLength(24).IsRequired();
            e.Property(x => x.Quantity).HasPrecision(28, 12);
            e.Property(x => x.Price).HasPrecision(28, 12);
            e.Property(x => x.QuoteQuantity).HasPrecision(28, 12);
            e.Property(x => x.Fee).HasPrecision(28, 12);
            e.HasIndex(x => new { x.OwnerUserId, x.Kind, x.Symbol, x.ReferenceId }).IsUnique();
            e.HasIndex(x => new { x.OwnerUserId, x.OccurredUtc });
        });
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
